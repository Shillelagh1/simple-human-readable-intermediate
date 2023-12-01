using System.IO;
using commandline_arg_parse;
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

			compiler.Compile(body);

			return compiler;
		}

		// Compiler Data
		bool ignoreNonexistentCIs = false;
		string reqBody = "";

		// Compiler Methods
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

				// Comment the line that generated the following code, for everyones sake
				reqBody += $"; {GetStringFromBytes(line).Replace((char)0, ' ')}\n";

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
			return reqBody;
		}
		#endregion

		// Compiler Instructions
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

		#endregion
	}
}
