using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;

namespace SmartCOD {
	internal class Program {

		static int Main(string[] args) {

			// Check for input arguments.
			if (args.Length == 0) {
				Console.WriteLine("Usage: {0} build <output COD name> [code1 name] [code2 name]", Process.GetCurrentProcess().ProcessName);
				Console.WriteLine("   or: {0} load <input COD name> <SmartBox serial port name>", Process.GetCurrentProcess().ProcessName);
				return 1;
			}

			// What operation has been selected?
			switch (args[0].ToLowerInvariant()) {
				case "build":
					return Build(args);
				case "load":
					return Load(args);
				default:
					Console.Error.WriteLine("Unsupported operation '{0}'.", args[0]);
					return 1;
			}
		}

		/// <summary>
		/// Build a COD file from two code files (master and difference).
		/// </summary>
		static int Build(string[] args) {
			
			// We must have at least an output filename.
			if (args.Length < 2) {
				Console.Error.WriteLine("Output COD filename not specified.");
				return 1;
			}
			var fileOut = new FileInfo(args[1]);

			// Default filenames are "code1" and "code2"
			var fileMaster = new FileInfo("code1");
			var fileDiff = new FileInfo("code2");

			// If arguments are specified, use those as the input files.
			if (args.Length > 2) fileMaster = new FileInfo(args[2]);
			if (args.Length > 3) fileDiff = new FileInfo(args[3]);

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
		static int Load(string[] args) {

			// We must have at least an input filename.
			if (args.Length < 2) {
				Console.Error.WriteLine("Input COD filename not specified.");
				return 1;
			}
			var fileIn = new FileInfo(args[1]);

			if (!fileIn.Exists) {
				Console.Error.WriteLine("Code file '{0}' does not exist.", fileIn.Name);
				return 1;
			}

			// Open the serial port.
			if (args.Length < 3) {
				Console.Error.WriteLine("SmartBox serial port name not specified.");
				return 1;
			}
			if (!Array.Exists(SerialPort.GetPortNames(), pn=>pn.ToLowerInvariant() == args[2].ToLowerInvariant())) {
				Console.Error.WriteLine("Invalid serial port name '{0}'.", args[2]);
				return 1;
			}

			using (var box = new SmartBox(args[2])) {

                // Display the SmartBox version information and credits.
                Console.WriteLine(box.GetCredits().Replace("\r", Environment.NewLine).Trim());

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

			return 0;

		}
	}
}
