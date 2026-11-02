using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

internal static class InteropBoundaryChecks
{
    internal static void Run(string gameAssembly, string pluginAssembly)
    {
        using var game = new Index(gameAssembly);
        using var plugin = new Index(pluginAssembly);
        int hooks = 0;
        void Hook(string owner, string target, string managedType, string managedMethod, bool attribute = false)
        {
            var native = game.Method(owner, target);
            var hook = plugin.Method("OldenEra.RetaliationHints." + managedType, managedMethod);
            if (hook.Signature.ReturnType != "Void") throw new Exception("Observer must not replace native results: " + managedType);
            foreach (var parameter in hook.Parameters)
            {
                string expected = parameter.Name switch {
                    "__instance" => owner,
                    "__result" => native.Signature.ReturnType,
                    _ => null
                };
                if (parameter.Name.Length > 2 && parameter.Name.StartsWith("__") && int.TryParse(parameter.Name[2..], out int i))
                    expected = native.Signature.ParameterTypes[i];
                if (expected != null && expected != parameter.Type)
                    throw new Exception($"Hook parameter mismatch: {managedType}.{managedMethod} {parameter.Name}: {parameter.Type} / {expected}");
                if (parameter.Type.Contains("DamageUiParams")) throw new Exception("Non-blittable DamageUiParams cannot be a detour parameter");
            }
            if (attribute)
            {
                var type = plugin.Reader.GetTypeDefinition(plugin.Types["OldenEra.RetaliationHints." + managedType]);
                bool found = false;
                foreach (var handle in type.GetCustomAttributes())
                {
                    var value = plugin.Reader.GetCustomAttribute(handle);
                    if (value.Constructor.Kind != HandleKind.MemberReference) continue;
                    var ctor = plugin.Reader.GetMemberReference((MemberReferenceHandle)value.Constructor);
                    if (plugin.Name(ctor.Parent) != "HarmonyLib.HarmonyPatch") continue;
                    var blob = plugin.Reader.GetBlobReader(value.Value);
                    if (blob.ReadUInt16() != 1) throw new Exception("Invalid attribute encoding");
                    var nativeType = blob.ReadSerializedString()?.Split(',')[0];
                    var method = blob.ReadSerializedString();
                    found = nativeType == owner && method == target;
                }
                if (!found) throw new Exception("Compiled Harmony target differs from the verified native method: " + managedType);
            }
            hooks++;
        }
        Hook("Hex.Session.Battle.BhBattleUnitInfoView", "bhhb", "PreviewCapture+DamageShown", "Postfix", true);
        Hook("eqd", "bhpx", "PreviewCapture+CounterChecked", "Postfix", true);
        Hook("epl", "bhdb", "PreviewCapture+PreviewHidden", "Postfix", true);
        Hook("eqd", "bhpm", "CombatAudit", "BeginCommand");
        Hook("evj", "biel", "CombatAudit", "Queued");
        Hook("eux", "biea", "CombatAudit", "OpeningStarted");
        Hook("euz", "biea", "CombatAudit", "CounterStarted");
        Hook("ezp", "bjpx", "CombatAudit", "CounterExecuting");
        Hook("ezv", "bjrp", "CombatAudit", "BeforeDamage");
        Hook("eoi", "bglr", "CombatAudit", "DamageEvent");
        if (plugin.Method("OldenEra.RetaliationHints.CombatAudit", "EndCommand").Signature.ReturnType != "Void"
            || plugin.Method("OldenEra.RetaliationHints.CombatAudit", "AfterDamage").Signature.ReturnType != "Void")
            throw new Exception("Finalizers must preserve the game's exceptions");
        Console.WriteLine($"PASS: compiled native observer boundaries ({hooks} hooks; no game code loaded)");
    }

    private sealed class Index : IDisposable
    {
        private readonly FileStream stream;
        private readonly PEReader pe;
        internal readonly MetadataReader Reader;
        internal readonly Dictionary<string, TypeDefinitionHandle> Types = new();
        internal Index(string path)
        {
            stream = File.OpenRead(path); pe = new(stream); Reader = pe.GetMetadataReader();
            foreach (var h in Reader.TypeDefinitions) Types[Name(h)] = h;
        }
        internal string Name(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                var t = Reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                var parent = t.GetDeclaringType();
                string prefix = parent.IsNil ? Reader.GetString(t.Namespace) : Name(parent) + "+";
                return prefix + (prefix.Length > 0 && !prefix.EndsWith('+') ? "." : "") + Reader.GetString(t.Name);
            }
            var r = Reader.GetTypeReference((TypeReferenceHandle)handle);
            return Reader.GetString(r.Namespace) + (r.Namespace.IsNil || Reader.GetString(r.Namespace).Length == 0 ? "" : ".") + Reader.GetString(r.Name);
        }
        internal (MethodSignature<string> Signature, List<(string Name, string Type)> Parameters) Method(string type, string name)
        {
            var t = Reader.GetTypeDefinition(Types[type]);
            var m = t.GetMethods().Select(Reader.GetMethodDefinition).Single(m => Reader.GetString(m.Name) == name);
            var signature = m.DecodeSignature(new Names(), this);
            var parameters = m.GetParameters().Select(Reader.GetParameter).Where(p => p.SequenceNumber > 0)
                .Select(p => (Reader.GetString(p.Name), signature.ParameterTypes[p.SequenceNumber - 1])).ToList();
            return (signature, parameters);
        }
        public void Dispose() { pe.Dispose(); stream.Dispose(); }
    }
    private sealed class Names : ISignatureTypeProvider<string, Index>
    {
        public string GetArrayType(string element, ArrayShape shape) => element + "[]";
        public string GetByReferenceType(string element) => element + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fn";
        public string GetGenericInstantiation(string generic, ImmutableArray<string> args) => generic + "<" + string.Join(",", args) + ">";
        public string GetGenericMethodParameter(Index context, int index) => "M" + index;
        public string GetGenericTypeParameter(Index context, int index) => "T" + index;
        public string GetModifiedType(string modifier, string unmodified, bool required) => unmodified;
        public string GetPinnedType(string element) => element;
        public string GetPointerType(string element) => element + "*";
        public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
        public string GetSZArrayType(string element) => element + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte raw) => Qualified(reader.GetString(reader.GetTypeDefinition(handle).Namespace), reader.GetString(reader.GetTypeDefinition(handle).Name));
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte raw) => Qualified(reader.GetString(reader.GetTypeReference(handle).Namespace), reader.GetString(reader.GetTypeReference(handle).Name));
        public string GetTypeFromSpecification(MetadataReader reader, Index context, TypeSpecificationHandle handle, byte raw) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        private static string Qualified(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
    }
}
