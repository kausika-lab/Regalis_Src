// Adds the customer-portal profile fields to UserDetailDto (Regalis.Application) and
// fills them in UserAdminService.ToDetail (Regalis.Infrastructure).
// Usage: DtoPatcher <original api dir> <output dir> <shared framework dir>
using Mono.Cecil;
using Mono.Cecil.Cil;

var src = args[0];
var dst = args[1];
var fx = args[2];
Directory.CreateDirectory(dst);

// name -> where the type comes from: "profile" = same-named ProfileDto property, "user" = User property type
var fields = new (string Name, string TypeFrom)[]
{
    ("Gstin", "profile"), ("DateOfBirth", "profile"), ("Gender", "user"),
    ("AddressLine1", "profile"), ("AddressLine2", "profile"), ("City", "profile"),
    ("State", "profile"), ("PostalCode", "profile"), ("Country", "profile"),
    ("IsProfileComplete", "profile"),
};

DefaultAssemblyResolver Resolver(params string[] dirs)
{
    var r = new DefaultAssemblyResolver();
    foreach (var d in r.GetSearchDirectories()) r.RemoveSearchDirectory(d);
    foreach (var d in dirs) r.AddSearchDirectory(d);
    return r;
}
ReaderParameters Read(DefaultAssemblyResolver r, string path) => new()
{
    AssemblyResolver = r,
    ReadSymbols = File.Exists(Path.ChangeExtension(path, ".pdb")),
};

// ---- Regalis.Application: properties on UserDetailDto ----
var appPath = Path.Combine(src, "Regalis.Application.dll");
var app = AssemblyDefinition.ReadAssembly(appPath, Read(Resolver(src, args[3], fx), appPath));
var mod = app.MainModule;
var dto = mod.GetType("Regalis.Application.Administration.UserDetailDto") ?? throw new("UserDetailDto not found");
var profile = mod.GetType("Regalis.Application.Auth.ProfileDto") ?? throw new("ProfileDto not found");
var userDef = mod.AssemblyResolver.Resolve(mod.AssemblyReferences.Single(a => a.Name == "Regalis.Domain"))
    .MainModule.GetType("Regalis.Domain.Entities.Identity.User") ?? throw new("User not found");

foreach (var (name, from) in fields)
{
    if (dto.Properties.Any(p => p.Name == name)) throw new($"UserDetailDto already has {name} - already patched?");
    var type = from == "profile"
        ? profile.Properties.Single(p => p.Name == name).PropertyType
        : mod.ImportReference(userDef.Properties.Single(p => p.Name == name).PropertyType);

    var field = new FieldDefinition($"<{name}>k__BackingField", FieldAttributes.Private, type);
    dto.Fields.Add(field);
    const MethodAttributes acc = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

    var get = new MethodDefinition("get_" + name, acc, type);
    var g = get.Body.GetILProcessor();
    g.Emit(OpCodes.Ldarg_0); g.Emit(OpCodes.Ldfld, field); g.Emit(OpCodes.Ret);

    var set = new MethodDefinition("set_" + name, acc, mod.TypeSystem.Void);
    set.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, type));
    var s = set.Body.GetILProcessor();
    s.Emit(OpCodes.Ldarg_0); s.Emit(OpCodes.Ldarg_1); s.Emit(OpCodes.Stfld, field); s.Emit(OpCodes.Ret);

    dto.Methods.Add(get);
    dto.Methods.Add(set);
    dto.Properties.Add(new PropertyDefinition(name, PropertyAttributes.None, type) { GetMethod = get, SetMethod = set });
}
app.Write(Path.Combine(dst, "Regalis.Application.dll"), new WriterParameters { WriteSymbols = app.MainModule.HasSymbols });
Console.WriteLine($"Application: added {fields.Length} properties to UserDetailDto");

// ---- Regalis.Infrastructure: set them in UserAdminService.ToDetail ----
var infraPath = Path.Combine(src, "Regalis.Infrastructure.dll");
var infra = AssemblyDefinition.ReadAssembly(infraPath, Read(Resolver(dst, src, args[3], fx), infraPath));
var imod = infra.MainModule;
var svc = imod.GetType("Regalis.Infrastructure.Services.UserAdminService") ?? throw new("UserAdminService not found");
var toDetail = svc.Methods.Single(m => m.Name == "ToDetail");
var body = toDetail.Body;
var ret = body.Instructions.Last();
var made = ret.Previous;
if (ret.OpCode != OpCodes.Ret || made.OpCode != OpCodes.Newobj || ((MethodReference)made.Operand).DeclaringType.FullName != dto.FullName)
    throw new("ToDetail does not end in `return new UserDetailDto(...)` - layout changed, not patching");
if (body.Instructions.Any(i => i.Operand == ret || (i.Operand is Instruction[] t && t.Contains(ret))))
    throw new("A branch targets the final ret - not patching");

var patchedDto = imod.AssemblyResolver.Resolve(imod.AssemblyReferences.Single(a => a.Name == "Regalis.Application"))
    .MainModule.GetType(dto.FullName);
var user = toDetail.Parameters[0].ParameterType.Resolve();
var il = body.GetILProcessor();
foreach (var (name, _) in fields)
{
    il.InsertBefore(ret, il.Create(OpCodes.Dup));
    il.InsertBefore(ret, il.Create(OpCodes.Ldarg_0));
    il.InsertBefore(ret, il.Create(OpCodes.Callvirt, imod.ImportReference(user.Properties.Single(p => p.Name == name).GetMethod)));
    il.InsertBefore(ret, il.Create(OpCodes.Callvirt, imod.ImportReference(patchedDto.Properties.Single(p => p.Name == name).SetMethod)));
}
infra.Write(Path.Combine(dst, "Regalis.Infrastructure.dll"), new WriterParameters { WriteSymbols = infra.MainModule.HasSymbols });
Console.WriteLine($"Infrastructure: ToDetail now copies {fields.Length} fields");
