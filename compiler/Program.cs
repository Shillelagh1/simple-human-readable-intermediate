using System.IO;
using commandline_arg_parse;
using tether_signature_parser;
using System.Text;
using System.Reflection;

namespace Simple_Compiler
{
	public class Simple_ITD_Compile
	{
		public static int Main(string[] args)
		{
			args = CommandlineArgParse.CleanInputArgs(args);

			string inputFileName = "";

			// Grab input file
			if (args.Length > 0)
			{
				inputFileName = args[0];
			}
			if (inputFileName == "")
			{
				// No input file
				Console.WriteLine("FATAL: No input file specified!");
				return -1;
			}

			// Load the input file
			byte[] fileBody = File.ReadAllBytes(inputFileName);
			Console.WriteLine($"Loaded {fileBody.Length} bytes of '{inputFileName}'");

			// Clean the input
			fileBody = Clean_Input(fileBody);

			// Write the output clean file if the '-clean' flag is set
			if (CommandlineArgParse.IsFlagSet(args, "-clean"))
			{
				File.WriteAllBytes("clean.bin", fileBody);
			}

			var outFile = EasilyCompile(fileBody).Result();
			File.WriteAllText(CommandlineArgParse.GetFlagResult(args, "-out", "out.asm"), outFile);

			return 0;
		}

		public static Simple_ITD_Compile EasilyCompile(byte[] body)
		{
			var compiler = new Simple_ITD_Compile();

			compiler.GetAllPkgSigs();

			compiler.Compile(body);

			return compiler;
		}

		// Compiler Data
		bool ignoreNonexistentCIs = false;
		string reqBody = "global main\n";
		string textBody = "";
		string procName = "_start";
		int procStackDisplacement = 0;
		string procTextBody = "";
		Dictionary<string, ITDType> procLocalVars = new Dictionary<string, ITDType>();
		List<TetherSignature> types = new List<TetherSignature>();


		// Compiler Methods
		public void GetAllPkgSigs(){
			if(!Directory.Exists("pkg")){
				throw new Exception("CERROR -- No 'pkg' directory! Create one and populate it with packages");
			}
			
			foreach (var file in Directory.EnumerateFiles("pkg", "*.bin"))
			{
				var signatureList = TetherSignatureParser.LoadSignatureFile(file);
				foreach(var i in signatureList){
					types.Add(i);
				}
			}
		}

		public void Compile(byte[] body)
		{
			// Reflections info for compiler instructions
			MethodInfo[] methods = typeof(Simple_ITD_Compile).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

			var lines = SplitByteField(body, '\n');

			for (int i = 0; i < lines.Count; i++)
			{
				byte[] line = lines[i];

				var parts = SplitByteField(line, (char)0);
				byte[] cmd = parts[0];
				List<byte[]> args = new List<byte[]>(parts);
				args.RemoveAt(0);

				// Ignore 0 length lines (duh)
				if (line.Length <= 0)
				{
					continue;
				}

				if (cmd[0] == '#')
				{
					// Strip the first character ('#') from the cmd
					cmd = cmd.Skip(1).ToArray();

					// Run the compiler instruction :]
					RunCompilerInstruction(GetStringFromBytes(cmd), args);
				}
			}
		}

		private void RunCompilerInstruction(string name, List<byte[]> args){
			MethodInfo? m = typeof(Simple_ITD_Compile).GetMethod($"CI_{name.ToUpper()}", BindingFlags.Instance|BindingFlags.NonPublic);
			if(m==null){
				if(ignoreNonexistentCIs){
					Console.WriteLine($"CWARNING -- No compiler instruction '{name.ToUpper()}'");
					return;
				}
				throw new Exception($"CERROR -- No compiler instruction '{name.ToUpper()}'");
			}
			m.Invoke(this, new object[]{args});
		}

		// Utilities
		#region 
		public static byte[] Clean_Input(byte[] body)
		{
			List<byte> newBodyA = new List<byte>();
			List<byte> newBodyB = new List<byte>();

			// Clean whitespaces and comments (Ignoring literals)
			bool inLiteral = false;
			bool inComment = false;
			for (int i = 0; i < body.Length; i++)
			{
				char c = (char)body[i];
				if (c == '\'')
				{
					inLiteral = !inLiteral;
					continue;
				}
				if (c == ';')
				{
					inComment = true;
				}
				if (c == 10)
				{
					inComment = false;
				}

				if (!inComment)
				{
					if (!inLiteral)
					{
						if (!(Char.IsWhiteSpace(c) && c != 10))
						{
							newBodyA.Add((byte)c);
						}
					}
					else
					{
						newBodyA.Add((byte)c);
					}
				}
			}

			// Clear chained whitespaces
			for (int i = 0; i < newBodyA.Count; i++)
			{
				char c = (char)newBodyA[i];
				if (c == 10)
				{
					if (i < newBodyA.Count - 1)
					{
						if (newBodyA[i + 1] == 10)
						{
							continue;
						}
					}
				}

				newBodyB.Add((byte)c);
			}

			return newBodyB.ToArray();
		}

		private static List<byte[]> SplitByteField(byte[] body, char seperator)
		{
			List<byte[]> lines = new List<byte[]>();
			Encoding.ASCII.GetString(body).Split(seperator).ToList().ForEach((x) =>
			{
				List<byte> byteList = new List<byte>();
				x.ToList().ForEach((x) =>
				{
					byteList.Add((byte)x);
				});
				lines.Add(byteList.ToArray());
			});

			return lines;
		}

		private static string GetStringFromBytes(byte[] body)
		{
			return Encoding.ASCII.GetString(body);
		}

		public string Result()
		{
			return $"bits 64\n\n{reqBody}section .text\n{textBody}";
		}

		public TetherSignature GetSignatureByName(string name){
			foreach(var sig in types){
				if(sig.name == name){
					return sig;
				}
			}

			throw new Exception($"CERROR -- No signature with name '{name}' exists!");
		}
		#endregion

		// Compiler Instructions
		// https://github.com/Shillelagh1/simple-human-readable-intermediate/wiki
		#region 
		private void CI_EXTREQ(List<byte[]> args)
		{
			string extFileName = $"exts/{GetStringFromBytes(args[0])}.ext";
			if (File.Exists(extFileName))
			{
				string reqFileText = File.ReadAllText(extFileName);
				reqBody += reqFileText;
				if (!(reqFileText.Last() == '\n'))
				{
					reqBody += "\n\n";
				}
			}
			else
			{
				throw new Exception($"CERROR -- Cannot find file '{extFileName}'");
			}
		}

		private void CI_BADCI(List<byte[]> args){
			ignoreNonexistentCIs = true;
		}

		private void CI_ENDBADCI(List<byte[]> args){
			ignoreNonexistentCIs = false;
		}

		private void CI_PROC(List<byte[]> args){
			procName = GetStringFromBytes(args[0]);
			procLocalVars = new Dictionary<string, ITDType>();
			procStackDisplacement = 0;
			procTextBody = "";
		}

		private void CI_LOCALARR(List<byte[]> args){
			string rawName = GetStringFromBytes(args[1]);
			string name = rawName.Trim('&');
			var arrayElementType = GetSignatureByName(GetStringFromBytes(args[0]));
			int reflevel = rawName.Count(f => f == '&');
			procLocalVars.Add(name, new ITDType(arrayElementType, reflevel, true));

			procStackDisplacement += 8;
			procTextBody += "mov rdi, 5\n";
			procTextBody += "call malloc\n";
			procTextBody += $"mov [rdi-{procStackDisplacement}], rax\n\n";
		}

		private void CI_ENDPROC(List<byte[]> args){
			int toOffset = 16 * (int)(Math.Floor(procStackDisplacement / 16.0)+1);
			string header = $"{procName}:\n";
			header += "push rbp\n";
			header += "mov rbp, rsp\n";
			header += $"sub rsp, {toOffset}\n";

			procTextBody = header + procTextBody;
			textBody += procTextBody;
		}

		#endregion
	}

	public class ITDType{
		TetherSignature typesig;
		int referenceLevel;
		bool isArray;

		public ITDType(TetherSignature typesig, int referenceLevel, bool isArray){
			this.typesig = typesig;
			this.referenceLevel = referenceLevel;
			this.isArray = isArray;
		}
	}
}
