using System;

namespace SmartCOD {

	public class SmartBox : IDisposable {

		public enum Command : byte {
			Blank = 0,
			Version = 1,
			Reset = 2,
			NameCode = 3,
			CodeName = 4,
			MultipleSetup = 5,
			MultipleRead = 6,
			MultipleServer = 7,
			Credits = 9,
			WriteMotors = 10,
			ReadMotors = 11,
			MotorForward = 12,
			MotorReverse = 13,
			MotorHalt = 14,
			MotorPower = 15,
			WriteOutputs = 20,
			OutputPower = 21,
			GetSensors = 22,
			CheckSensors = 23,
			SetBitHigh = 28,
			SetBitLow = 29,
			ReadADCReg = 30,
			WriteADCReg = 31,
			ReadACIAReg = 32,
			WriteACIAReg = 33,
			ReadVIAReg = 34,
			WriteVIAReg = 35,
			SetVIAHigh = 36,
			SetVIALow = 37,
			ReadADC = 40,
			ReadADCs = 41,
			ForcedADCRead = 42,
			HighResADC = 44,
			LowResADC = 45,
			ReadResolution = 47,
			DownloadData = 50,
			UploadData = 52,
			ExecuteCode = 54,
			StoreByte = 55,
			ReadByte = 56,
			ReadRAMSize = 57,
			ExtendCall = 59,
			SetClock = 60,
			ReadClock = 61,
			ReadTopmem = 62,
			WriteTopmem = 63,
			ReadLomem = 64,
			WriteLomem = 65,
			ReadHimem = 66,
			WriteHimem = 67,
			ReadInputs = 90,
			ReadBit = 91,
		};

		public enum MotorDirection : byte {
			Stopped = 0,
			Forward = 1,
			Backward = 2,
		}

		private bool disposedValue;

		internal Port port;

		public SmartBox(Port port) {
			this.port = port;
		}

		public SmartBox(string portName) : this(new Port(portName)) {
		}

		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					this.port?.Dispose();
					this.port = null;
				}
				disposedValue = true;
			}
		}

		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Read the operating system version number
		/// </summary>
		/// <returns>The OS version number as a decimal.</returns>
		public decimal GetVersion() {
			this.port.Write((byte)Command.Version);
			return this.port.ReadUInt16() / 1000m;
		}

		/// <summary>
		/// Reset SmartBox
		/// </summary>
		/// <param name="hard">Set to <c>true</c> to clear battery-backed RAM.</param>
		/// <remarks>Before sending anymore codes create a small delay while SmartBox resets various parts of hardware</remarks>
		public void Reset(bool hard) {
			this.port.Write((byte)Command.Reset);
			this.port.Write((byte)(hard ? 255 : 254));
		}

		/// <summary>
		/// To obtain the operating system call number where the name is known
		/// </summary>
		/// <param name="osCallName">OS call name</param>
		/// <returns>Operating system call number</returns>
		public byte GetNameCode(string osCallName) {
			this.port.Write((byte)Command.NameCode);
			this.port.Write(osCallName);
			return this.port.ReadByte();
		}

		/// <summary>
		/// Obtain the name associated with an operating system call
		/// </summary>
		/// <param name="osCallNumber">OS call number</param>
		/// <returns>OS call name</returns>
		public string GetCodeName(byte osCallNumber) {
			this.port.Write((byte)Command.CodeName);
			this.port.Write((byte)osCallNumber);
			return this.port.ReadString();
		}


		/// <summary>
		/// Set the values that will be returned by MultipleRead
		/// </summary>
		/// <param name="analogueChannel1">Return status of analogue channel 1</param>
		/// <param name="analogueChannel2">Return status of analogue channel 2</param>
		/// <param name="analogueChannel3">Return status of analogue channel 3</param>
		/// <param name="analogueChannel4">Return status of analogue channel 4</param>
		/// <param name="digitalInputs">Return status of digital inputs</param>
		/// <param name="digitalOutputs">Return status of digital outputs</param>
		/// <param name="motorOutputs">Return status of motor outputs</param>
		public void MultipleSetup(bool analogueChannel1, bool analogueChannel2, bool analogueChannel3, bool analogueChannel4, bool digitalInputs, bool digitalOutputs, bool motorOutputs) {
			this.port.Write((byte)Command.MultipleSetup);
			this.port.Write((byte)(analogueChannel1 ? 1 : 0));
			this.port.Write((byte)(analogueChannel2 ? 1 : 0));
			this.port.Write((byte)(analogueChannel3 ? 1 : 0));
			this.port.Write((byte)(analogueChannel4 ? 1 : 0));
			this.port.Write((byte)(digitalInputs ? 1 : 0));
			this.port.Write((byte)(digitalOutputs ? 1 : 0));
			this.port.Write((byte)(motorOutputs ? 1 : 0));
		}

		/// <summary>
		/// Returns multiple readings as defined using MultipleSetup.
		/// </summary>
		/// <param name="length">The number of readings being returned</param>
		/// <returns>Bytes as defined by MultipleSetup</returns>
		public byte[] MultipleRead(int length) {
			this.port.Write((byte)Command.MultipleRead);
			return this.port.ReadBytes(length);
		}

		/// <summary>
		/// Returns the copyright string
		/// </summary>
		/// <returns>The copyright message and OS details</returns>
		public string GetCredits() {
			this.port.Write((byte)Command.Credits);
			return this.port.ReadString(0);
		}

		/// <summary>
		/// Writes a byte to the motor drivers
		/// </summary>
		/// <param name="value">Value to write</param>
		/// <remarks>
		/// Each of the four motor outputs has two bits encoded into this value, bits 0 and 1 are for motor a, bits 2 and 3 are for motor b, bits 4 and 5 are for motor c and bits 6 and 7 are for motor d, setting both bits to 0 will stop the motor, setting the low bit high and high bit low will make the motor go forward and setting the high bit high and the low bit low will make it go backwards.
		/// Using this call automatically stops all pulsing of motors
		/// </remarks>
		public void WriteMotors(byte value) {
			this.port.Write((byte)Command.WriteMotors);
			this.port.Write((byte)value);
		}

		/// <summary>
		/// Writes the direction of each motor to the motor drivers
		/// </summary>
		/// <param name="a">The direction of motor A</param>
		/// <param name="b">The direction of motor B</param>
		/// <param name="c">The direction of motor C</param>
		/// <param name="d">The direction of motor D</param>
		public void WriteMotors(MotorDirection a, MotorDirection b, MotorDirection c, MotorDirection d) {
			this.WriteMotors((byte)(((byte)a & 3) << 0 | ((byte)b & 3) << 2 | ((byte)c & 3) << 4 | ((byte)d & 3) << 6));
		}

		/// <summary>
		/// Read the state of the motor drivers
		/// </summary>
		/// <returns>The value returned is the same value as what would be sent to WriteMotors</returns>
		public byte ReadMotors() {
			this.port.Write((byte)Command.ReadMotors);
			return this.port.ReadByte();
		}

		/// <summary>
		/// Reads the direction of each motor from the motor drivers
		/// </summary>
		/// <param name="a">The direction of motor A</param>
		/// <param name="b">The direction of motor B</param>
		/// <param name="c">The direction of motor C</param>
		/// <param name="d">The direction of motor D</param>
		public void ReadMotors(out MotorDirection a, out MotorDirection b, out MotorDirection c, out MotorDirection d) {
			var value = this.ReadMotors();
			a = (MotorDirection)((value >> 0) & 3);
			b = (MotorDirection)((value >> 2) & 3);
			c = (MotorDirection)((value >> 4) & 3);
			d = (MotorDirection)((value >> 6) & 3);
		}

		public ushort ReadLomem() {
			this.port.Write((byte)Command.ReadLomem);
			return this.port.ReadUInt16();
		}

		public ushort ReadHimem() {
			this.port.Write((byte)Command.ReadHimem);
			return this.port.ReadUInt16();
		}

		public void DownloadData(ushort address, byte[] data) {
			this.port.Write((byte)Command.DownloadData);
			this.port.Write((ushort)address);
			this.port.Write((ushort)data.Length);
			this.port.Write((byte[])data);
		}

		public void ExecuteCode(ushort address, byte a, byte x, byte y) {
			this.port.Write((byte)Command.ExecuteCode);
			this.port.Write((ushort)address);
			this.port.Write((byte)a);
			this.port.Write((byte)x);
			this.port.Write((byte)y);
		}

		/// <summary>
		/// Reads a byte from the digital inputs port
		/// </summary>
		/// <returns>Byte read from the digital inputs port</returns>
		public byte ReadInputs() {
			this.port.Write((byte)Command.ReadInputs);
			return this.port.ReadByte();
		}

		private readonly static string[] sensorNames = new string[] {
			null,
			null,
			string.Empty,
			"Temp",
			null,
			null,
			"Volts",
			"Temp",
			"Volts",
			"Temp",
			"Sound",
			"PH",
			string.Empty,
			"Position",
			string.Empty,
			string.Empty,
			string.Empty,
			"Light",
			string.Empty,
			string.Empty,
			string.Empty,
			"Humidity",
			"Sound",
			"Light",
			"Sound",
			string.Empty,
			"Atmos",
			"Light",
			"User",
			"Adaptor",
			"Temp",
			"LGate",
			string.Empty,
			"Temp",
		};

		public static bool SensorIsPresent(byte id) {
			if (id < sensorNames.Length) {
				return sensorNames[id] != null;
			} else {
				return false;
			}
		}

		public static string GetSensorName(byte id) {
			if (id < sensorNames.Length) {
				return sensorNames[id];
			} else {
				return null;
			}
		}
		

	}
}
