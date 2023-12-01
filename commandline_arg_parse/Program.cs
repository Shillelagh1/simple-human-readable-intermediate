namespace commandline_arg_parse{
	public static class CommandlineArgParse{
		public static string[] CleanInputArgs(string[] args){
			string[] output = new string[args.Length];
			for(int i = 0; i < args.Length; i++){
				output[i] = args[i].Trim().ToLower();
			}
			return output;
		}
		public static bool IsFlagSet(string[] args, string flag){
			foreach(string i in args){
				if(i == flag){
					return true;
				}
			}
			return false;
		}
		public static string GetFlagResult(string[] args, string flag, string defaultVal = ""){
			for(int i = 0; i < args.Length; i++){
				if(args[i] == flag){
					if(i < args.Length - 1){
						return args[i+1];
					}
				}
			}
			return defaultVal;
		}
	}
}
