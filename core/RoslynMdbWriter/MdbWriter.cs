using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Cci;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis;

namespace Mono.CompilerServices.SymbolWriter
{
	class MdbWriter : ISymUnmanagedWriter5, IPdbWriter
	{
		MonoSymbolWriter msw;
		// int nextLocalIndex;

		Dictionary<string,SymbolDocumentWriterImpl> documents = new Dictionary<string, SymbolDocumentWriterImpl> ();

		#region ISymUnmanagedWriter2 implementation

		MetadataWriter writer;

		public void Initialize (object emitter, string filename, object ptrIStream, bool fullBuild)
		{
			var writerField = typeof(PdbMetadataWrapper).GetRuntimeFields().First((f) => f.Name == "_writer");
			writer = (MetadataWriter)writerField.GetValue(emitter);

			msw = new MonoSymbolWriter(new UnsafeComStreamWrapper((ComMemoryStream)ptrIStream));
		}

		public void OpenMethod (uint method)
		{
			var sm = new SourceMethodImpl (writer.GetMethodDefinition (method).Name, (int)method);
			msw.OpenMethod (null, 0, sm);
		}

		public ISymUnmanagedDocumentWriter DefineDocument (string url, ref Guid language, ref Guid languageVendor, ref Guid documentType)
		{
			SymbolDocumentWriterImpl doc;
			if (!documents.TryGetValue (url, out doc)) {
				SourceFileEntry entry = msw.DefineDocument (url);
				CompileUnitEntry comp_unit = msw.DefineCompilationUnit (entry);
				doc = new SymbolDocumentWriterImpl (comp_unit);
				documents.Add (url, doc);
			}
			return doc;
		}

		public void CloseMethod ()
		{
			// nextLocalIndex = 0;
			msw.CloseMethod ();
		}

		public uint OpenScope (uint startOffset)
		{
			return (uint)msw.OpenScope ((int)startOffset);
		}

		public void CloseScope (uint endOffset)
		{
			msw.CloseScope ((int)endOffset);
		}

		public void Close ()
		{
			var heapBuilder = (MetadataHeapsBuilder)typeof(MetadataWriter).GetRuntimeFields ().First ((f) => f.Name == "heaps").GetValue (writer);

			Guid moduleVersionId = Guid.Empty;
			var guidIndex = (Dictionary<Guid, int>)typeof(MetadataHeapsBuilder).GetRuntimeFields ().First ((f) => f.Name == "_guids").GetValue (heapBuilder);
			foreach (var pair in guidIndex) {
				if (pair.Value == 1) {
					moduleVersionId = pair.Key;
					break;
				}
			}
			msw.WriteSymbolFile (moduleVersionId);
		}

		#region Used by Roslyn but not implemented

		public void SetSymAttribute (uint parent, string name, uint data, IntPtr signature)
		{
			//Roslyn is calling this with name=="MD2" but not sure if .mdb has anything that could use this data
		}

		public void SetUserEntryPoint (uint entryMethod)
		{
			//.mdb doesn't have counterpart for this
		}

		public void UsingNamespace (string fullName)
		{
			//Not used in Mono runtime but seems to be in .mdb not sure what is use for this
		}

		public void DefineConstant2 (string name, VariantStructure value, uint sigToken)
		{
			//Mdb doesn't support constant values.
			//Constant values are lost at compile time and are not part of ILs/Stack.
			//So in case of PDB this values are stored inside PDB so debugger can display this values in IDE.
			//But in case of MDB. IDE displays this values from SyntaxTree knowledge. This has drawback
			//this value can be seen only by hovering mouse over constant name in code and can't be visible in Locals as
			//in case of PDB.(in theory TypeSystem/SyntaxTree could inform Locals about constants in current method)
		}

		#endregion

		public void GetDebugInfo (ref ImageDebugDirectory ptrIDD, uint dataCount, out uint dataCountPtr, IntPtr data)
		{
			// PdbWriter.GetContentId will use this function.
			//
			// Data has the following structure:
			// struct RSDSI                     
			// {
			//     DWORD dwSig;                 // "RSDS"
			//     GUID guidSig;                // GUID
			//     DWORD age;                   // age
			//     char szPDB[0];               // zero-terminated UTF8 file name passed to the writer
			// };

			dataCountPtr = 4 + 16 + 4 + 1;
		}

		public void DefineSequencePoints (ISymUnmanagedDocumentWriter document, uint count, uint[] offsets, uint[] lines, uint[] columns, uint[] endLines, uint[] endColumns)
		{
			msw.SetMethodUnit (((ICompileUnit)document).Entry);
			var doc = (SymbolDocumentWriterImpl)document;
			var file = doc != null ? doc.Entry.SourceFile : null;
			for (int n = 0; n < count; n++) {
				if (n > 0 && offsets [n] == offsets [n - 1] && lines [n] == lines [n - 1] && columns [n] == columns [n - 1])
					continue;
				msw.MarkSequencePoint ((int)offsets [n], file, (int)lines [n], (int)columns [n], (int)endLines [n], (int)endColumns [n], lines [n] == 0xfeefee);
			}
		}

		public void DefineLocalVariable2 (string name, uint attributes, uint sigToken, uint addrKind, uint addr1, uint addr2, uint addr3, uint startOffset, uint endOffset)
		{
			// msw.DefineLocalVariable (nextLocalIndex++, name);
			msw.DefineLocalVariable ((int)addr1, name);
		}

		#endregion

		#region Unimplemented methods which are also not used by Roslyn

		public class NotUsedInRoslynException : Exception
		{
			public NotUsedInRoslynException () :
				base ("Method was called in MdbWriter that was not used by Roslyn in past. Need to implement this method.")
			{

			}
		}

		public void DefineLocalVariable (string name, uint attributes, uint sig, IntPtr signature, uint addrKind, uint addr1, uint addr2, uint startOffset, uint endOffset)
		{
			throw new NotUsedInRoslynException ();
		}

		public void DefineField (uint parent, string name, uint attributes, uint sig, IntPtr signature, uint addrKind, uint addr1, uint addr2, uint addr3)
		{
			throw new NotUsedInRoslynException ();
		}

		public void DefineGlobalVariable2 (string name, uint attributes, uint sigToken, uint addrKind, uint addr1, uint addr2, uint addr3)
		{
			throw new NotUsedInRoslynException ();
		}

		public void OpenNamespace (string name)
		{
			throw new NotUsedInRoslynException ();
		}

		public void CloseNamespace ()
		{
			throw new NotUsedInRoslynException ();
		}

		public void RemapToken (uint oldToken, uint newToken)
		{
			throw new NotUsedInRoslynException ();
		}

		public void Initialize2 (object emitter, string tempfilename, object ptrIStream, bool fullBuild, string finalfilename)
		{
			throw new NotUsedInRoslynException ();
		}

		public void DefineConstant (string name, object value, uint sig, IntPtr signature)
		{
			throw new NotUsedInRoslynException ();
		}

		public void Abort ()
		{
			throw new NotUsedInRoslynException ();
		}

		public void SetMethodSourceRange (ISymUnmanagedDocumentWriter startDoc, uint startLine, uint startColumn, object endDoc, uint endLine, uint endColumn)
		{
			throw new NotUsedInRoslynException ();
		}

		public void SetScopeRange (uint scopeID, uint startOffset, uint endOffset)
		{
			throw new NotUsedInRoslynException ();
		}

		public void DefineParameter (string name, uint attributes, uint sequence, uint addrKind, uint addr1, uint addr2, uint addr3)
		{
			throw new NotUsedInRoslynException ();
		}

		public void DefineGlobalVariable (string name, uint attributes, uint sig, IntPtr signature, uint addrKind, uint addr1, uint addr2, uint addr3)
		{
			throw new NotUsedInRoslynException ();
		}

		#region ISymUnmanagedWriter5 implementation

		public void _VtblGap1_30 ()
		{
			throw new NotUsedInRoslynException ();
		}

		public void OpenMapTokensToSourceSpans ()
		{
			var assembly = writer.Context.Module.AsAssembly;
			if (assembly != null && assembly.Kind == OutputKind.WindowsRuntimeMetadata) {
				//I guess Mono doesn't care about .winmdobj file generation
			} else {
				throw new NotUsedInRoslynException ();
			}
		}

		public void CloseMapTokensToSourceSpans ()
		{
			var assembly = writer.Context.Module.AsAssembly;
			if (assembly != null && assembly.Kind == OutputKind.WindowsRuntimeMetadata) {
				//I guess Mono doesn't care about .winmdobj file generation
			} else {
				throw new NotUsedInRoslynException ();
			}
		}

		public void MapTokenToSourceSpan (uint token, ISymUnmanagedDocumentWriter document, uint startLine, uint startColumn, uint endLine, uint endColumn)
		{
			var assembly = writer.Context.Module.AsAssembly;
			if (assembly != null && assembly.Kind == OutputKind.WindowsRuntimeMetadata) {
				//I guess Mono doesn't care about .winmdobj file generation
			} else {
				throw new NotUsedInRoslynException ();
			}
		}

		#endregion

		#endregion

		// IPdbWriter

		public int __SetPath()
		{
			throw new NotImplementedException();
		}

		public int __OpenMod()
		{
			throw new NotImplementedException();
		}

		public int __CloseMod()
		{
			throw new NotImplementedException();
		}

		public int __GetPath()
		{
			throw new NotImplementedException();
		}

		public void GetSignatureAge(out uint sig, out uint age)
		{
			sig = 0;
			age = 0;
		}
	}

	class SourceMethodImpl: IMethodDef
	{
		string name;
		int token;

		public SourceMethodImpl (string name, int token)
		{
			this.name = name;
			this.token = token;
		}

		public string Name {
			get { return name; }
		}

		public int Token {
			get { return token; }
		}
	}

	class SymbolDocumentWriterImpl: ISymUnmanagedDocumentWriter, ISourceFile, ICompileUnit
	{
		CompileUnitEntry comp_unit;

		public SymbolDocumentWriterImpl (CompileUnitEntry comp_unit)
		{
			this.comp_unit = comp_unit;
		}

		SourceFileEntry ISourceFile.Entry {
			get { return comp_unit.SourceFile; }
		}

		public CompileUnitEntry Entry {
			get { return comp_unit; }
		}

		#region ISymUnmanagedDocumentWriter implementation

		public void SetSource (uint sourceSize, byte[] source)
		{
		}

		public void SetCheckSum (Guid algorithmId, uint checkSumSize, byte[] checkSum)
		{
		}

		#endregion
	}
}
