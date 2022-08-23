using System;
using System.IO;
using System.Collections.Generic;

public class Options {
	private class Opt {
		public bool   flag;
		public bool   mandatory;
		public string defVal;
		public string help;
		public Opt() {}
		public Opt(bool flag, bool mandatory, string defVal, string help) {
			this.flag = flag;
			this.mandatory = mandatory;
			this.defVal = defVal;
			this.help = help;
		}
	}
	private class IdOpt : Opt {
		public string id;
		public IdOpt(string id, bool flag, bool mandatory, string defVal, string help) {
			this.id = id;
			this.flag = flag;
			this.mandatory = mandatory;
			this.defVal = defVal;
			this.help = help;
		}
	}


	private static Dictionary<string, string> config = new Dictionary<string,string>();


	private static readonly Dictionary<string,Opt> namedOptions = new Dictionary<string,Opt> {
		{"bg",		new Opt(false, false, null, "Background map image")},
		{"mask",	new Opt(false, false, null, "Mask image to subtract before analyze")},
		{"log",		new Opt(false, false, null, "Log file name")},
		{"mult",	new Opt(false, false, null, "Output values multiplication coefficient")},
	};
	private static readonly IdOpt[] unnamedOptions = new IdOpt[] {
		new IdOpt("imgdir",		false, true, null, "Analyze images diretory"),
		new IdOpt("outdir",		false, true, null, "Output directory"),
	};
	
	
	public static void usage() {
		Console.Write("Usage: WeatherStats.exe [options]");
		foreach (IdOpt opt in unnamedOptions) {
			if (opt.mandatory) {
				Console.Write(" {0}", opt.id);
			}
		}
		Console.WriteLine(" [options]");
		Console.WriteLine("Options:");
		foreach (KeyValuePair<string, Opt> entry in namedOptions) {
			Opt opt = entry.Value;
			Console.WriteLine("  {0}{1}\t\t{2} - {3}", (opt.flag ? "-" : "--"), entry.Key, (opt.mandatory ? "*mandatory" : ("def: " + ((opt.defVal != null) ? opt.defVal : "(empty)"))), opt.help);
		}
		Console.WriteLine();
	}
	
	
	public static string get(string id) {
		return config[id];
	}
	
	public static bool getBool(string id) {
		return Boolean.Parse(config[id]);
	}
	
	public static int getInt(string id) {
		if (id.StartsWith("#")) {
			return int.Parse(config[id.Substring(1)], System.Globalization.NumberStyles.HexNumber);
		}
		return int.Parse(config[id]);
	}
	
	public static double getDouble(string id) {
		return Double.Parse(config[id]);
	}
	
	
	private static int resetToDefaults() {
		config.Clear();
		int mandatoryCount = 0;
		foreach (KeyValuePair<string, Opt> entry in namedOptions) {
			Opt opt = entry.Value;
			if (opt.mandatory) {
				mandatoryCount++;
			} else {
				config[entry.Key] = opt.defVal;
			}
		}
		foreach (IdOpt opt in unnamedOptions) {
			if (opt.mandatory) {
				mandatoryCount++;
			} else {
				config[opt.id] = opt.defVal;
			}
		}
		return mandatoryCount;
	}
	
	public static bool expectedMandatory(int count) {
		Console.WriteLine("Expected {0} mandatory arguments", count);
		usage();
		return false;
	}
	
	
	public static bool parse(string[] args) {
		int mandatoryCount = resetToDefaults();
		if (args.Length < mandatoryCount) {
			return expectedMandatory(mandatoryCount);
		}
		
		int unnamedIdx = 0;
		bool stopNamed = false;
		for (int argIdx = 0; argIdx < args.Length; argIdx++) {
			string arg = args[argIdx];
			bool isFlag, isNamed;
			if (arg == "--") {
				if (mandatoryCount > 0) {
					return expectedMandatory(mandatoryCount);
				}
				stopNamed = true;
				continue;
			}
			
			if (!stopNamed && arg.StartsWith("--")) {
				isFlag = false;
				isNamed = true;
				arg = arg.Substring(2);
				//Console.WriteLine("NAMED: {0} -> {1}", args[argIdx], arg);
			} else if (!stopNamed && arg.StartsWith("-")) {
				isFlag = true;
				isNamed = true;
				arg = arg.Substring(1);
				//Console.WriteLine("NAMED FLAG: {0} -> {1}", args[argIdx], arg);
			} else {
				isFlag = false;
				isNamed = false;
				//Console.WriteLine("UNNAMED: {0}", arg);
			}
			
			Opt opt;
			string val;
			if (isNamed) {
				// NAMED.
				if (!namedOptions.TryGetValue(arg, out opt)) {
					Console.WriteLine("Unsupported argument: {0}", args[argIdx]);
					usage();
					return false;
				}
				if (isFlag) {
					val = opt.defVal;
				} else {
					argIdx++;
					if (argIdx >= args.Length) {
						Console.WriteLine("Argument {0} has no value", args[argIdx-1]);
						usage();
						return false;
					}
					val = args[argIdx];
					//Console.WriteLine("NAMED VALUE: {0}", val);
				}
			} else {
				// UNNAMED.
				if (unnamedIdx >= unnamedOptions.Length) {
					Console.WriteLine("No more unnamed arguments allowed: {0}", args[argIdx]);
					usage();
					return false;
				}
				val = arg;
				IdOpt idOpt = unnamedOptions[unnamedIdx++];
				arg = idOpt.id;
				opt = idOpt;
			}
			
			config[arg] = val;
			if (opt.mandatory) {
				mandatoryCount--;
			}
			//Console.WriteLine("SAVED: [{0}] = [{1}]{2}", arg, val, opt.mandatory ? " (mandatory)" : "");
		}
		
		return true;
	}
}
