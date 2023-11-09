using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SmartCOD {
	internal class Program {

		static int Main(string[] args) {

			// Check for input arguments.
			if (args.Length == 0) {
				Console.WriteLine(Properties.Resources.CommandLine);
				return 1;
			}

			// We'll pull arguments from a queue.
			var argQueue = new Queue<string>(args);

			// Parsed option values.
			var options = new Dictionary<string, string>();

			// What's our return value?
			int returnValue = 0;

			// Work through the argument list.
			while (returnValue == 0 && argQueue.Count > 0) {
				var arg = argQueue.Dequeue();
				if (arg.StartsWith("-")) {
					// It's an option to a subsequent command.
					if (arg.Length == 1) {
						continue;
					} else if (argQueue.Count < 1) {
						Console.Error.WriteLine("No option value following option '{0}'.", arg);
						return 1;
					} else {
						var optionKey = arg.Substring(1).ToLowerInvariant();
						var optionValue = argQueue.Dequeue();
						// Handle special options first, then store the option value directly if otherwise unsupported.
						switch (optionKey) {
							case "unset":
								// Remove the option specified by the value.
								optionValue = optionValue.ToLowerInvariant();
								if (options.ContainsKey(optionValue)) {
									options.Remove(optionValue);
								}
								break;
							default:
								// Remove the old value.
								if (options.ContainsKey(optionKey)) {
									options.Remove(optionKey);
								}
								// Store the new value.
								options.Add(optionKey, optionValue);
								break;
						}
					}
				} else {
					// It's a command.
					switch (arg.ToLowerInvariant()) {
						case "build":
							returnValue = Build(options);
							break;
						case "load":
							returnValue = Load(options);
							break;
						case "list":
							returnValue = List(options);
							break;
						case "reset":
							returnValue = Reset(options);
							break;
						case "call":
							returnValue = Call(options, argQueue);
							break;
						default:
							Console.Error.WriteLine("Unsupported command '{0}'.", arg);
							returnValue = 1;
							break;
					}
				}
			}

			return returnValue;
		}

		/// <summary>
		/// Try to get a SmartBox instance based on the supplied command-line options.
		/// </summary>
		/// <param name="options">The current array of options to use.</param>
		/// <param name="box">Returns the SmartBox instance if it could be found.</param>
		/// <returns>True on success, false on failure.</returns>
		static bool TryGetSmartBox(Dictionary<string, string> options, out SmartBox box) {
			
			box = default;

			// Open the serial port.
			if (!options.ContainsKey("port")) {
				Console.Error.WriteLine("SmartBox serial port option -port <portname> not specified.");
				return false;
			}

			var portName = options["port"];
			if (!Array.Exists(SerialPort.GetPortNames(), pn => pn.ToLowerInvariant() == portName.ToLowerInvariant())) {
				Console.Error.WriteLine("Invalid serial port name '{0}'.", portName);
				return false;
			}

			box = new SmartBox(portName);
			return true;
		}

		/// <summary>
		/// Try to parse a string value as a byte.
		/// </summary>
		/// <param name="value">The value to try to parse.</param>
		/// <param name="result">The parsed byte value.</param>
		/// <returns><c>true</c> if the byte value could be parsed, <c>false</c> otherwise.</returns>
		static bool TryParseByte(string value, out byte result) {
			return byte.TryParse(value, out result);
		}

		/// <summary>
		/// Try to parse a string value as a ushort.
		/// </summary>
		/// <param name="value">The value to try to parse.</param>
		/// <param name="result">The parsed ushort value.</param>
		/// <returns><c>true</c> if the ushort value could be parsed, <c>false</c> otherwise.</returns>
		static bool TryParseUShort(string value, out ushort result) {
			return ushort.TryParse(value, out result);
		}

		/// <summary>
		/// Try to parse a string value as a uint.
		/// </summary>
		/// <param name="value">The value to try to parse.</param>
		/// <param name="result">The parsed byte value.</param>
		/// <returns><c>true</c> if the uint value could be parsed, <c>false</c> otherwise.</returns>
		static bool TryParseUInt(string value, out uint result) {
			return uint.TryParse(value, out result);
		}

		/// <summary>
		/// Try to parse a string value as a string.
		/// </summary>
		/// <param name="value">The value to try to parse.</param>
		/// <param name="result">The parsed string value.</param>
		/// <returns><c>true</c> if the string value could be parsed, <c>false</c> otherwise.</returns>
		static bool TryParseString(string value, out string result) {
			result = value;
			return true;
		}

		static bool TryParseArgument(CallParameterType type, string value, out object result) {

			bool success = false;
			object primitive = default;

			switch (type) {
				case CallParameterType.Byte:
					if (success = TryParseByte(value, out byte b)) {
						primitive = b;
					}
					break;
				case CallParameterType.Word:
					if (success = TryParseUShort(value, out ushort w)) {
						primitive = w;
					}
					break;
				case CallParameterType.Int:
					if (success = TryParseUInt(value, out uint i)) {
						primitive = i;
					}
					break;
				case CallParameterType.String:
				case CallParameterType.NulTerminatedString:
					if (success = TryParseString(value, out string s)) {
						primitive = s;
					}
					break;
			}
			
			result = primitive;
			return success;
		}

		/// <summary>
		/// Build a COD file from two code files (master and difference).
		/// </summary>
		static int Build(Dictionary<string, string> options) {
			
			// We must have at least an output filename.
			if (!options.ContainsKey("cod")) {
				Console.Error.WriteLine("Output file option -cod <filename> not specified.");
				return 1;
			}
			var fileOut = new FileInfo(options["cod"]);

			// Default filenames are "code1" and "code2"
			var fileMaster = new FileInfo("code1");
			var fileDiff = new FileInfo("code2");

			// If arguments are specified, use those as the input files.
			if (options.ContainsKey("master")) fileMaster = new FileInfo(options["master"]);
			if (options.ContainsKey("diff")) fileDiff = new FileInfo(options["diff"]);

			// Check if the master file exists.
			if (!fileMaster.Exists) {
				Console.Error.WriteLine("Master file '{0}' does not exist.", fileMaster.Name);
				return 1;
			}

			// Check if the difference file exists.
			if (!fileDiff.Exists) {
				Console.Error.WriteLine("Difference file '{0}' does not exist.", fileDiff.Name);
				return 1;
			}

			// Check if the master and difference files are the same length.
			if (fileMaster.Length != fileDiff.Length) {
				Console.Error.WriteLine("Master and difference files are different sizes.");
				return 1;
			}

			// Sanity check for too-large or too-small files.
			if (fileMaster.Length > ushort.MaxValue) {
				Console.Error.WriteLine("Master and difference files are too large.");
				return 1;
			} else if (fileMaster.Length < 3) {
				Console.Error.WriteLine("Master and difference files are too small.");
				return 1;
			}

			// Load the data.
			var dataMaster = File.ReadAllBytes(fileMaster.FullName);
			var dataDiff = File.ReadAllBytes(fileDiff.FullName);

			// Check the offset to the execcall entrypoint is the same in both.
			if (dataDiff[0] != dataMaster[0] || dataDiff[1] != dataMaster[1]) {
				Console.Error.WriteLine("Offset to execcall differs in master and difference files.");
				return 1;
			}

			// Check execcall is within the file.
			var execCall = (ushort)(dataMaster[0] + dataMaster[1] * 256);
			if (execCall >= dataMaster.Length) {
				Console.Error.WriteLine("execcall entrypoint is beyond the end of the file.");
				return 1;
			}

			// Generate the output file.
			using (var outputFile = new BinaryWriter(File.Create(fileOut.FullName))) {

				// Start with the base code.
				outputFile.Write(dataMaster);

				// Write the relocation bitmap.
				for (int i = 0; i < execCall; i += 8) {
					byte mask = 0;
					for (int j = 0; j < 8; ++j) {
						if (i + j < dataMaster.Length && dataDiff[i + j] != dataMaster[i + j]) {
							mask |= (byte)(1 << j);
						}
					}
					outputFile.Write(mask);
				}

			}
			return 0;
		}

		/// <summary>
		/// Load and run a COD file on a connected SmartBox.
		/// </summary>
		static int Load(Dictionary<string, string> options) {

			// We must have at least an input filename.
			if (!options.ContainsKey("cod")) {
				Console.Error.WriteLine("Input file option -cod <filename> not specified.");
				return 1;
			}
			var fileIn = new FileInfo(options["cod"]);

			if (!fileIn.Exists) {
				Console.Error.WriteLine("Code file '{0}' does not exist.", fileIn.Name);
				return 1;
			}


			if (!TryGetSmartBox(options, out SmartBox box)) {
				return 1;
			} else {
				using (box) {

					// Get free memory.
					var lomem = box.ReadLomem();
					var himem = box.ReadHimem();

					if (fileIn.Length > (himem - lomem)) {
						Console.Error.WriteLine("'{0}' is too large to fit in SmartBox memory ({1} bytes available).", fileIn.Name, himem - lomem);
						return 1;
					}

					// Read code file.
					var fileData = File.ReadAllBytes(fileIn.FullName);

					// Send to SmartBox.
					int chunkSize = 128;
					const string progressMessage = "\rDownloading {0} into SmartBox at &{1:X4} ({2:P})...";
					for (int offset = 0; offset < fileData.Length; offset += chunkSize) {
						Console.Write(progressMessage, fileIn.Name, lomem, (float)offset / (float)fileData.Length);
						int length = Math.Min(chunkSize, fileData.Length - offset);
						box.port.Write((byte)SmartBox.Command.DownloadData);
						box.port.Write((ushort)(lomem + offset));
						box.port.Write((ushort)length);
						box.port.Write(fileData, offset, length);
					}
					Console.WriteLine(progressMessage + " OK", fileIn.Name, lomem, 1);

					// Execute the code file.
					ushort entrypoint = (ushort)(lomem + fileData[0] + fileData[1] * 256);
					Console.Write("Executing &{0:X4}...", entrypoint);
					box.ExecuteCode(entrypoint, 0, (byte)entrypoint, (byte)(entrypoint / 256));
					Console.WriteLine(" OK");
				}
			}
			return 0;

		}

		/// <summary>
		/// Show a listing of jobs on the SmartBox
		/// </summary>
		static int List(Dictionary<string, string> options) {
			if (!TryGetSmartBox(options, out SmartBox box)) {
				return 1;
			} else {
				using (box) {
					string s;
					for (int i = 0; i < byte.MaxValue; ++i) {
						if (!string.IsNullOrEmpty(s = box.GetCodeName((byte)i))) {
							Console.WriteLine("{0}: {1}", i, s);
						}
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// Reset the SmartBox
		/// </summary>
		static int Reset(Dictionary<string, string> options) {

			byte reset = 254;

			if (options.ContainsKey("reset")) {
				switch (options["reset"].ToLowerInvariant()) {
					case "hard":
						reset = 255;
						break;
					case "soft":
						reset = 254;
						break;
					default:
						if (!TryParseByte(options["reset"], out reset)) {
							Console.Error.WriteLine("Could not parse -reset value '{0}'.", options["reset"]);
							return 1;
						}
						break;
				}
			}

			if (!TryGetSmartBox(options, out SmartBox box)) {
				return 1;
			} else {
				using (box) {
					box.Reset(reset);
				}
			}
			return 0;
		}

		enum CallParameterType {
			Byte,
			Word,
			Int,
			String,
			NulTerminatedString,
		}

		static bool TryGetCallParameterType(string typeString, out CallParameterType type) {
			if (typeString == null) {
				type = default;
				return false;
			}
			switch (typeString.Trim().ToUpperInvariant()) {
				case "B":
				case "?":
				case "&":
					type = CallParameterType.Byte;
					return true;
				case "W":
				case "??":
				case "&&":
					type = CallParameterType.Word;
					return true;
				case "I":
				case "!":
				case "%":
					type = CallParameterType.Int;
					return true;
				case "S":
				case "$":
					type = CallParameterType.String;
					return true;
				case "S0":
				case "$0":
					type = CallParameterType.NulTerminatedString;
					return true;
				default:
					type = default;
					return false;
			}
		}

		static bool TryGetCallParameterTypes(string[] typeStrings, out CallParameterType[] types) {
			types = new CallParameterType[typeStrings.Length];
			for (int i = 0; i < typeStrings.Length; ++i) {
				if (!TryGetCallParameterType(typeStrings[i], out types[i])) {
					types = default;
					return false;
				}
			}
			return true;
		}

		static bool TryGetCallParameterTypes(string typeStrings, out CallParameterType[] types) {
			if (typeStrings == null) {
				types = default;
				return false;
			} else if (typeStrings.Trim().Length == 0) {
				types = new CallParameterType[] { };
				return true;
			}
			return TryGetCallParameterTypes(typeStrings.Split(','), out types);
		}

		static readonly Dictionary<string, string> CallParameters = new Dictionary<string, string>();

		/// <summary>
		/// Call a job on the SmartBox.
		/// </summary>
		static int Call(Dictionary<string, string> options, Queue<string> arguments) {

			// We always need a job name to call.
			if (arguments.Count < 1) {
				Console.Error.WriteLine("Job name not specified in call.");
				return 1;
			}
			var jobName = arguments.Dequeue();

			// We also need a parameter definition.
			string parameters = null;
			if (options.ContainsKey("params")) {
				parameters = options["params"];
			} else if (CallParameters.ContainsKey(jobName.ToLowerInvariant())) {
				parameters = CallParameters[jobName.ToLowerInvariant()];
			}

			if (string.IsNullOrEmpty(parameters)) {
				Console.Error.WriteLine("Parameters not defined for call '{0}'.", jobName);
				return 1;
			}

			// Trim the parameter definition.
			parameters = parameters.Trim().TrimStart('(').TrimEnd(')');

			// Check parameters are in form (in--out)
			var parameterParts = parameters.Split(new[] { "--" }, StringSplitOptions.None);
			if (parameterParts.Length != 2) {
				Console.Error.WriteLine("Parameter definition not correctly defined for call '{0}'.", jobName);
				return 1;
			}

			// Convert parameter type definitions from user-supplied string.
			if (!TryGetCallParameterTypes(parameterParts[0], out CallParameterType[] inputParameters)) {
				Console.Error.WriteLine("Parameter input definition not correctly specified for call '{0}'.", jobName);
				return 1;
			}
			if (!TryGetCallParameterTypes(parameterParts[1], out CallParameterType[] outputParameters)) {
				Console.Error.WriteLine("Parameter output definition not correctly specified for call '{0}'.", jobName);
				return 1;
			}

			// Check if we have enough input arguments.
			if (arguments.Count < inputParameters.Length) {
				Console.Error.WriteLine("Not enough input arguments specified for call '{0}' (expected: '{1}').", jobName, parameterParts[0]);
				return 1;
            }

			// Parse input arguments.
			var inputArguments = new KeyValuePair<CallParameterType, object>[inputParameters.Length];
			for (int i = 0; i < inputParameters.Length; ++i) {
				var arg = arguments.Dequeue();
				if (TryParseArgument(inputParameters[i], arg, out object o)) {
					inputArguments[i] = new KeyValuePair<CallParameterType, object>(inputParameters[i], o);
				} else {
					Console.Error.WriteLine("Could not parse argument '{0}' as '{1}'.", arg, inputParameters[i]);
					return 1;
				}
			}

			// At this point we're ready to call the routine.
			if (!TryGetSmartBox(options, out SmartBox box)) {
				return 1;
			} else {
				using (box) {
					
					// Get the job code.
					var jobCode = box.GetNameCode(jobName);
					if (jobCode == 0 && jobName.ToLowerInvariant() != "blank") {
						Console.Error.WriteLine("Could not get job code for job '{0}'.", jobName);
						return 1;
					}
					
					// Write the job code.
					box.port.Write((byte)jobCode);

					// Send all of the input arguments.
					foreach (var arg in inputArguments) {
						switch (arg.Key) {
							case CallParameterType.Byte:
								box.port.Write((byte)arg.Value);
								break;
							case CallParameterType.Word:
								box.port.Write((ushort)arg.Value);
								break;
							case CallParameterType.Int:
								box.port.Write((uint)arg.Value);
								break;
							case CallParameterType.String:
								box.port.Write((string)arg.Value);
								break;
							case CallParameterType.NulTerminatedString:
								box.port.Write((string)arg.Value, 0);
								break;
						}
					}

					// Fetch the output data.
					foreach (var type in outputParameters) {
						object data = default;
						switch (type) {
							case CallParameterType.Byte:
								data = box.port.ReadByte();
								break;
							case CallParameterType.Word:
								data = box.port.ReadUInt16();
								break;
							case CallParameterType.Int:
								data = box.port.ReadUInt32();
								break;
							case CallParameterType.String:
								data = box.port.ReadString();
								break;
							case CallParameterType.NulTerminatedString:
								data = box.port.ReadString(0);
								break;
							default:
								throw new InvalidOperationException();
						}
                        Console.WriteLine(data.ToString().Replace("\r", Environment.NewLine));
                    }
                }
			}

			// Clear the parameter list.
			options.Remove("params");

			// Remember the parameters for later.
			if (CallParameters.ContainsKey(jobName.ToLowerInvariant())) {
				CallParameters.Remove(jobName.ToLowerInvariant());
			}
			CallParameters.Add(jobName.ToLowerInvariant(), parameters);

			return 0;
		}
	}
}
