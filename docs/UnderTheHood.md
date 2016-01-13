# Under the hood

IncrementalCompiler for Unity3D was my fun-project for 2016 new year's holiday.
At that time I was thinking about slow compilation speed of unity3d-mono and
wondering if it is possible to make it faster with the minimum effort.
After a couple of hours digging, it turned out feasible to make it.

## Making incremental compiler

Microsoft / Mono C# compiler doesn't support incremental compilation until now.
They had "/incremental" option once in old times but lost it already.
Because C# compiler is really fast and C# language itself is good to
keep compiler run fast, incremental compilation has not been at the first priority.
Also community keep there project small and like to separate big project into smaller ones
to handle this problem indirectly.

Unity3D uses mono C# infrastructure. It seems hard to make compiler work faster
because Unity3D still uses old slow Mono 3 and doesn't provide an easy way to divide
big project into small one. So I decided to make incremental compiler for Unity3D.

### Unity build process

Unity3D makes three projects from Assets directory if your project has only C# sources.

  - Assembly-CSharp-firstpass
    - Consists of scripts in Plugins directory.
    - No dependency.
    - It's rare to build this project because these sources are not modified.
  - Assembly-CSharp
    - Consists of almost sources in Assets.
      (except for sources in Plugins and Editor directory)
    - Depends on Assembly-CSharp-firstpass
    - This project is always built because these sources are main place for common work.
  - Assembly-CSharp-Editor
   - Consists of scripts in Editor directory.
   - Depends on Assembly-CSharp
   - This project is always built not because these sources are modified but by building
     dependent Assembly-CSharp.

[Unity Manual: Special Folders and Script Compilation Order](http://docs.unity3d.com/Manual/ScriptCompileOrderFolders.html)

### How to replace a builtin compiler with a new one.

At first, Replacing mono compiler in unity directory with new one was considered.
But it is not an easy way for users and also intrusive way to affect whole projects.
However [alexzzzz](https://bitbucket.org/alexzzzz/unity-c-5.0-and-6.0-integration/src)
found a smart way to workaround this as following:

```csharp
[InitializeOnLoad]
public static class CSharp60SupportActivator {
  static CSharp60SupportActivator() {
    var list = GetSupportedLanguages();
    list.RemoveAll(language => language is CSharpLanguage);
    list.Add(new CustomCSharpLanguage());
  }
  private static List<SupportedLanguage> GetSupportedLanguages() {
    var fieldInfo = typeof(ScriptCompilers).GetField("_supportedLanguages", ...);
    var languages = (List<SupportedLanguage>)fieldInfo.GetValue(null);
    return languages;
  }
}
```

It is an internal feature for Unity3D and cannot be accessed from external DLLs.
But to make it, he renamed plugin DLL to one of internal friend DLL names,
`Unity.PureCSharpTests`.

### Roslyn

[Roslyn](https://github.com/dotnet/roslyn) is new open-source project providings
C# and Visual Basic compilers. With this project it's really easy to use features
that compiler can provide like parsing, analysing and even compiling itself.

Just with [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis/),
compiler can be written without any hard work.

```csharp
// Minimal C# compiler
Assembly Build(string[] sourcePaths, string[] referencePaths, string[] defines) {
  var assemblyName = Path.GetRandomFileName();
  var syntaxTrees = sourcePaths.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
  var references = referencePaths.Select(file => MetadataReference.CreateFromFile(file));
  var compilation = CSharpCompilation.Create(assemblyName, syntaxTrees, references);
  using (var ms = new MemoryStream())
    compilation.Emit(ms);
}
```

### Incremental compiler

Roslyn C# compiler does two steps to compile from API view.
First one is parsing step. It loads sources and parse it.
Second one is emitting step. From compiler's view, most of work is done here
such as semantic analysis, optimization and code generation.
Because library users cannot access internal phase in emitting step,
incremental compiler is written in a simple way like:

Full compilation for first time build.
```csharp
var syntaxTrees = sourcePaths.Select(file => /* PARSE */);
var references = referencePaths.Select(file => /* LOAD */);
var compilation = CSharpCompilation.Create(syntaxTrees, references);
compilation.Emit(ms);           
```

Incremental compilation for subsequent builds.
```csharp
compliation = compliation.RemoveReferences(oldLoadedReferences)
                         .AddReferences(load(newReference))
                         .RemoveSyntaxTrees(oldSourceSyntaxTrees)
                         .AddSyntaxTrees(parse(newSource))
compilation.Emit(ms);
```

By telling changes in a project to compilation object, rolsyn can use
pre-parsed syntax trees and some informations that I wish.

### Compile server

Ok. Keeping compilation object and reusing can make an incremental compiler.
But where can we put this object on? Everytime Unity3D want to build DLL, it
invokes C# compiler.
C# compiler is running awhile, exits and leaves built DLL for Unity3D, which means
it should throw away compilation object.

We have two options to keep this object permanent:

 1. Save all information to disc and load them at next invocation.
 1. Make a compiler server. Let it alive while Unity3D is alive and
    every compile invocation will be forwared to it.

Because first one involves an IO intensive process which make work slow
and I don't know how to (de)serialize a compilation object of Roslyn,
second one is chosen.

When incremental compiler is requested to compile assembly,
it find a compiler server process. If there is no one, it spawns a compile server.
Compiler forwards a compilation request to this server and waits for results.
Compile server processes this request, return it to requester and keep
this intermediate object for subsequent requests.

To communicate between compile client and server,
[WCF on Named pipe](https://msdn.microsoft.com/en-us/library/ms733769%28v=vs.110%29.aspx) is used
to make this tool quickly even Mono doesn't support it.

## Modification for Unity3D/Mono

### Reuse prebuilt DLLs

When sources in Assembly-CSharp are edited, Unity3D builds Assembly-CSharp.
But at the same time Unity3D also build Assembly-CSharp-Editor
because it is dependent on Assembly-CSharp. It's a natural building protocol.

But most of time it is not useful.
If there is no changed in sources of Assembly-CSharp-Editor, it is considered
relatively safe to skip rebuilding Assembly-CSharp-Editor.

TODO: TEST & SHOW ERROR CASE
TODO: DETAILED DESCRIPTION OF OPTION

PrebuiltOutputReuse in IncrementalCompiler.xml

- WhenNoChange :
  When nothing changed in sources and references, reuse prebuilt results.
- WhenNoSourceChange :
  When nothing changed in sources and references (except update of some references),
  reuse prebuilt results.

### MDB instead of PDB

Roslyn emits PDB file as a debugging symbol. But Unity3D cannot understand PDB file
because it's based on mono compiler. To make unity3D get proper debugging information,
MDB file should be constructed and they already provided a tool to convert pdb to mdb and
jbevain update support output of visual studio 2015 [pdb2mdb](https://gist.github.com/jbevain/ba23149da8369e4a966f)

So simple process supporting unity3d is
 - Emit pdb via Roslyn
 - Convert pdb to mdb with pdb2mdb tool

But how about emitting mdb from Roslyn directly? it can save time for generating and converting pdb?
A guy at Xamarain already tried it but it is not updated now. So I grab his work and update it to work latest Roslyn.

### Renaming symbol for UnityVS debugging

[UnityVS](https://www.visualstudio.com/features/unitytools-vs)
does a lot of hard works to support Unity3D application debugging in Visual Studio.
.NET Assembly is required to provide debugging information to debugger such as
variable names, source information for IL code and etc but Visual Studio cannot
understand Mono assembly well enough to support debugging.
To deal with this problem, UnityVS examines assemblies carefully
and making use of common pattern of mono DLLs by itself.

.NET assembly built with Roslyn, however, is different with one with Mono.
Therefore UnityVS misses some information and cannot give enough debugging
information to users.

For example,

```csharp
IEnumerator TestCoroutine(int a, Func<int, string> b) {
    var v = a;
    yield return null;
    GetComponent<Text>().text = v.ToString();
    v += 1;
    yield return null; // Breakpoint here
    GetComponent<Text>().text = b(v);
}
```

UnityVS can show variable v in watch for DLL from Mono3.

```csharp
this    = "Text01 (Test01)"
v       = 11
a       = 10
b       = (trimmed)
```

UnityVS cannot show variable v in watch for DLL from Roslyn.

```csharp
this    = {Test01+<TestCoroutine>d__1}
Current = null
a       = 10
b       = (trimmed)
```

There is a difference between Mono and Roslyn for making name of local variables in iterator class.

```csharp
// Mono3
private sealed class <TestCoroutine>c__Iterator0 : IEnumerator<object>, IEnumerator, IDisposable {
	internal int a;
	internal int <v>__0;
	internal Func<int, string> b;
	internal Test01 <>f__this;
  // trimmed

// Roslyn
private sealed class <TestCoroutine>d__1 : IEnumerator<object>, IEnumerator, IDisposable {
  public int a;
  public Func<int, string> b;
  public Test01 <>4__this;
  private int <v>5__1;
  // trimmed
```

From disassembled UnityVS DLL, we can know how UnityVS determines which variable
need to be watched in debugging:

```csharp
// SyntaxTree.VisualStudio.Unity.Debugger.Properties.CSharpGeneratorEnvironment
public static bool IsCSharpGenerator(Value thisValue) {
  TypeMirror typeMirror = thisValue.GetTypeMirror();
  return typeMirror.Name.StartsWith("<") && typeMirror.Name.Contains("__Iterator");
}
public override UnityProperty[] VariableProperties() {
  return (from f in base.Type.GetInstanceFields()
          where !f.Name.StartsWith("<>") && !f.Name.StartsWith("<$")
          where f.Name.StartsWith("<")
          where f.Name.Contains(">__")
          select f into field
          select base.Field(field, field.Name.Substring(1, field.Name.IndexOf(">__", 1) - 1))
         ).ToArray<UnityProperty>();
}
```

UnityVS has following rules for looking for iterator.

 - The name of iterator class is form of <.\*__Iterator.\*
 - The name of local variable is form of <.+>__?

Only thing to do is renaming name of iterator class and field variable.
But how can we do it? There're two ways:

 1. Renaming those after building DLL with vanila roslyn.
 1. Building DLL with modified roslyn following UnityVS rules.

Because renaming after building need time-consuming DLL analysis process,
second one was chosen.

Check roslyn source related with this naming.
```csharp
internal static string MakeHoistedLocalFieldName(SynthesizedLocalKind kind, ...) {
  var result = PooledStringBuilder.GetInstance();
  var builder = result.Builder;
  builder.Append('<');
  if (localNameOpt != null)
      builder.Append(localNameOpt);
  builder.Append('>');
  if (kind == SynthesizedLocalKind.LambdaDisplayClass)
      builder.Append((char)GeneratedNameKind.DisplayClassLocalOrField);
  else if (kind == SynthesizedLocalKind.UserDefined)
      builder.Append((char)GeneratedNameKind.HoistedLocalField); // '5'
  else
      builder.Append((char)GeneratedNameKind.HoistedSynthesizedLocalField);
  builder.Append("__");
  builder.Append(slotIndex + 1);
  return result.ToStringAndFree();
}
```

Removing `builder.Append((char)GeneratedNameKind.HoistedLocalField);` can solve
the problem and Roslyn source is open and easy to modify.
But I don't want to maintain modified Roslyn source because it's too big.
Instead of roslyn source, rolsyn DLL is modified with Mono.Cecil like:

```csharp
static void Fix_GeneratedNames_MakeHoistedLocalFieldName(MethodDefinition method) {
  var il = method.Body.GetILProcessor();
  for (var i = 0; i < il.Body.Instructions.Count; i++) {
    var inst = il.Body.Instructions[i];
    if (inst.OpCode.Code == Code.Ldc_I4_S && (sbyte)inst.Operand == 53) {
      for (int j = 0; j < 4; j++)
        il.Remove(il.Body.Instructions[i + 2 - j]);
      break;
    }
  }
}
```
