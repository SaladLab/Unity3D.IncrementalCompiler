## RoslynMdbWriter

This modules let roslyn to emit an MDB debugging symbol file instead of PDB.
Most of code is based on [marek-safar](https://github.com/marek-safar)'s
[work](https://github.com/mono/roslyn/pull/4). But some works have been done
for handling changes of Roslyn.

#### More works done

- Make this assembly pretend to be a friend of roslyn assemblies to
  access internal classes like ISymUnmanagedWriter5.
  - Use roslyn internal strong name key.
  - Rename assembly to Roslyn.Test.Utilities. (one of friend assemblies of roslyn)
- Add MdbHelper to let pdbStream use MdbWriter.
- Add UnsafeComStreamWrapper to use ComMemoryStream like System.Stream.
- Update field name used for accessing private fields in roslyn classes.
  - PdbMetadataWrapper.writer -> PdbMetadataWrapper.\_writer
  - MetadataWriter.guidIndex -> MetadataWriter.heaps.\_guids
- Implment dummy MdbWriter.GetDebugInfo instead of returning nothing.
- Make MdbWriter implement IPdbWriter.
- MdbWriter.DefineLocalVariable2 uses addr1 as a local variable index rather than
  counting index with nextLocalIndex.
