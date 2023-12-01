using System;
using System.Text.RegularExpressions;
using tether_signature_parser;
using commandline_arg_parse;

namespace tether_typer
{
    public class Program
    {
        public static int Main(string[] args)
        {
            args = CommandlineArgParse.CleanInputArgs(args);
            GenerateObjectSignatures(File.ReadAllText(CommandlineArgParse.GetFlagResult(args, "--input", "in.th")), "base.bin", CommandlineArgParse.GetFlagResult(args, "--type-file", "out.bin"), CommandlineArgParse.IsFlagSet(args, "--show-base-objs"), CommandlineArgParse.IsFlagSet(args, "--show-final-objs"), CommandlineArgParse.GetFlagResult(args, "--spit-final"));
            return 0;
        }

        // TODO: Ensure there are no duplicate type names or members

        /// <summary>
        /// From the input tether file, 'program,' collect all classes and members, and write it to a machine readable intermediary format
        /// </summary>
        /// <param name="program"></param>
        public static void GenerateObjectSignatures(string program, string baseFile, string outFile, bool showBaseObjectSignatures = false, bool showFinalObjectSignatures = false, string spitFinal = "")
        {
            List<TetherSignature> objectSignatures = new List<TetherSignature>();

            /// Load all base object classes from the base file.
            if(File.Exists(baseFile)){
                Console.WriteLine(">>> Loading Base Object Types...");
                objectSignatures = TetherSignatureParser.LoadSignatureFile(baseFile);

                if(showBaseObjectSignatures){
                    TetherSignatureParser.VisualizeSignatures(objectSignatures);
                }
            }
            else{
                Console.WriteLine("WARNING: No base file found!");
            }


            Console.WriteLine(">>> Loading User Defined Object Types...");
            List<string> availableTypes = new List<string>();

            // Maybe don't use inline foreach loops, no matter how pretty they are.
            objectSignatures.ForEach((x)=>{availableTypes.Add(x.name);});

            /// Use a regex to collect all user defined classes from the program file. Collect all class names and add them to
            /// list of available user defined classes, 'availableParents'. Add all regex matches to a list of regex matches,
            /// and sort them in order of amount of '/' characters in the class name, from low to high. This serves to place
            /// the more fundamental classes before the classes dependent on them, eliminating the possibility of the typer
            /// falsly detecting an orphaned class.

            MatchCollection ms = Regex.Matches(program, EXPR.signatureDef, RegexOptions.Multiline);
            foreach (Match m in ms)
            {
                string e = m.Groups[1].ToString();
                e = Regex.Replace(e, @"\s", "");
                availableTypes.Add(e);
            }

            List<Match> sortedSignatureMatches = new List<Match>();
            foreach (Match m in ms)
            {
                sortedSignatureMatches.Add(m);
            }
            sortedSignatureMatches.Sort((Match a, Match b) => { return a.Groups[1].ToString().Count(x => x == '/') - b.Groups[1].ToString().Count(x => x == '/'); });

            /// Iterate over each class body, performing a regex on each. From each regex collect a list of class members.
            /// Finally, iterate over each member and split it into a type and name. Write each member tuple (containing the 
            /// type name and member name. ) to a list where it can later be written to the signature file.
            
            foreach(Match signatureMatch in sortedSignatureMatches){
                string signatureBody = signatureMatch.Groups[2].ToString();
                string signatureName = signatureMatch.Groups[1].ToString();
                string signatureParent = "";

                TetherSignature thisSignature = new TetherSignature(signatureName, TetherSignature.TetherSignatureClassification.complex);
                thisSignature.members = new List<Tuple<string, string, int>>();

                /// If this signature has a complex parent, and it's a valid type,
                /// find it and then add all of its members to us (inheritence)

                if(signatureName.LastIndexOf('/') > 0){
                    signatureParent = signatureName.Substring(0, signatureName.LastIndexOf('/'));
                    if(!availableTypes.Contains(signatureParent)){
                        throw new Exception(string.Format("No parent type '{0}' is available for object '{1}'", signatureParent, signatureName));
                    }
                    foreach(var i in objectSignatures){
                        if(i.name == signatureParent){
                            if(i.members == null){
                                break;
                            }
                            foreach(var k in i.members){
                                thisSignature.members.Add(k);
                            }
                            break;
                        }
                    }
                }

                MatchCollection memberMatches = Regex.Matches(signatureBody, EXPR.sigDefTargetCapture, RegexOptions.Multiline);
                int offset = 0;
                
                /// TODO: REWRITE THIS!!
                /// It adds several seconds to the execution time ( I think?) for even small programs,
                /// which is unnacceptable. Perhaps use a dictionary instead of iterating?

                // Get the new offset for child classes
                // Kid named bad syntax
                thisSignature.members.ForEach((x)=>{
                        foreach(var i in objectSignatures){
                        if(i.name == x.Item1){
                            offset += (int)i.GetImmediateLengthBytes();
                        } 
                    }  
                });

                // Finally process all members
                foreach(Match memberMatch in memberMatches){
                    string memberTypeName = memberMatch.Groups[1].ToString();
                    string memberName = memberMatch.Groups[2].ToString();
                    thisSignature.members.Add(new Tuple<string, string, int>(memberTypeName, memberName, offset));

                    // Get the length of this member, used to calculate the memory offset
                    // This makes sure the type exists too
                    uint l = 0;
                    foreach(var i in objectSignatures){
                        if(i.name == memberTypeName){
                            l = i.GetImmediateLengthBytes();
                            goto CONTINUE_GOOD_TYPE;
                        } 
                    }  
                    if(availableTypes.Contains(memberTypeName)){
                        l = 8;
                        goto CONTINUE_GOOD_TYPE;
                    }              
                    throw new Exception(string.Format("No type '{0}' for member '{2}' in object '{1}'", memberTypeName, signatureName, memberName));
                    CONTINUE_GOOD_TYPE:
                    offset += (int)l;
                }

                objectSignatures.Add(thisSignature);
            }

            /// Finally parse the object list to it's binary list. The binary file should occupy a per-byte
            /// format which can easily be understood by a hex editor. The file, separated by newline characters
            /// should contain each signature name, as well as each signatures' members and their corresponding names,
            /// type names, offsets, and lengths (all in bytes)

            if(showFinalObjectSignatures){
                TetherSignatureParser.VisualizeSignatures(objectSignatures);
            }

            if(spitFinal != ""){
                TetherSignatureParser.VisualizeSignatures(objectSignatures, spitFinal);
            }

            TetherSignatureParser.WriteSignatureFile(outFile, objectSignatures);
        }

        public static class EXPR
        {
            public static string signatureDef = @"^\s*object\s+([A-Za-z0-9/_]+)\s*{([^}]*)}";
            public static string sigDefTargetCapture = @"^\s*([A-Za-z0-9_/]+)\s+([A-Za-z0-9_]+)\s*$";
            public static string inQuotes = @"'.*'";

        }
    }
}