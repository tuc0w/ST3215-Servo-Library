using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class ServoController : IDisposable
{
    #region Global Variables
    private const int MaxSpeed = 2048;
    private SerialPort serialPort;
    private const int StepsPerRevolution = 4096; // Steps per 360°
    private const int ServoId = 1; // ID of the servo
    private bool disposed = false;
    #endregion

    #region Servo Addresses
    public const int PRESENT_POSITION_L = 56;
    public const int PRESENT_POSITION_H = 57;
    public const int PRESENT_SPEED_L = 58;
    public const int PRESENT_SPEED_H = 59;
    public const int PRESENT_LOAD_L = 60;
    public const int PRESENT_LOAD_H = 61;
    public const int PRESENT_VOLTAGE = 62;
    public const int PRESENT_TEMPERATURE = 63;
    public const int MOVING_STATUS = 66;
    public const int PRESENT_CURRENT_L = 69;
    public const int PRESENT_CURRENT_H = 70;
    #endregion

    public ServoController(string portName, int baudRate = 1000000)
    {
        serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        };
        serialPort.Open();
    }

    #region Movement Methods
    public void RotateToAngle(double angle, int speed)
    {
        if (angle < -90 || angle > 90)
        {
            throw new ArgumentOutOfRangeException("Angle must be between -90 and 90.");
        }

        if (speed < 0 || speed > MaxSpeed)
        {
            throw new ArgumentOutOfRangeException($"Speed must be between 0 and {MaxSpeed}.");
        }

        // Calculate position from angle
        int position = (int)((angle + 90) / 180 * StepsPerRevolution);
        position = Math.Max(0, Math.Min(4095, position)); // Limit to 0-4095

#if DEBUG
        Console.WriteLine($"Angle: {angle}, Calculated Position: {position}, Speed: {speed}");
#endif

        RotateToPosition(position, speed);
    }

    public void RotateToPosition(int position, int speed)
    {
        clearBuffer();

        if (position < 0 || position > 4095)
        {
            throw new ArgumentOutOfRangeException("Position must be between 0 and 4095.");
        }

        if (speed < 0 || speed > MaxSpeed)
        {
            throw new ArgumentOutOfRangeException("Speed must be between 0 and 4095.");
        }

        byte[] data = new byte[6];
        data[0] = (byte)(position & 0xFF);
        data[1] = (byte)((position >> 8) & 0xFF);
        data[2] = 0x00;
        data[3] = 0x00;
        data[4] = (byte)(speed & 0xFF);
        data[5] = (byte)((speed >> 8) & 0xFF);

        // INST WRITE (Instruction = 0x03)
        byte[] command = CreateCommand(ServoId, 0x2A, data, 0x03);

        serialPort.Write(command, 0, command.Length);

#if DEBUG
        Console.WriteLine("INST WRITE-Paket: " + BitConverter.ToString(command));
#endif

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (serialPort.BytesToRead == 0)
        {
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                throw new TimeoutException("Timeout: Servo did not answer in time.");
            }
            Thread.Sleep(1);
        }

#if DEBUG
        Console.WriteLine($"Bytes in buffer: {serialPort.BytesToRead}");
#endif

        byte[] response = new byte[6];
        int bytesRead = serialPort.Read(response, 0, response.Length);
        serialPort.DiscardInBuffer();

#if DEBUG
        Console.WriteLine($"Received answer: {BitConverter.ToString(response)}");
#endif
    }
    #endregion

    #region Status Methods
    public int ReadPos()
    {
        byte[] data = ReadStatus(PRESENT_POSITION_L, 2);
        return BitConverter.ToUInt16(data, 0);
    }

    public int ReadSpeed()
    {
        byte[] data = ReadStatus(PRESENT_SPEED_L, 2);
        return BitConverter.ToUInt16(data, 0);
    }

    public double ReadLoad()
    {
        byte[] data = ReadStatus(PRESENT_LOAD_L, 2);
        return BitConverter.ToUInt16(data, 0) / 10.0;
    }

    public double ReadVoltage()
    {
        byte[] data = ReadStatus(PRESENT_VOLTAGE, 1);
        return data[0] / 10.0;
    }

    public int ReadTemperature()
    {
        byte[] data = ReadStatus(PRESENT_TEMPERATURE, 1);
        return data[0];
    }

    public bool IsMoving()
    {
        byte[] data = ReadStatus(MOVING_STATUS, 1);
        return data[0] == 1;
    }

    public int ReadCurrent()
    {
        byte[] data = ReadStatus(PRESENT_CURRENT_L, 2);
        return BitConverter.ToUInt16(data, 0);
    }
    #endregion

    #region Command Methods
    private byte[] CreateCommand(int id, byte address, byte[] data, byte instruction)
    {
        int packetLength = instruction == 0x05 ? 6 : 7 + data.Length; // ACTION: 6, REG WRITE: 7 + Data
        byte[] packet = new byte[packetLength];

        // Header
        packet[0] = 0xFF;
        packet[1] = 0xFF;

        // ID, length and instruction
        packet[2] = (byte)id;
        packet[3] = (byte)(instruction == 0x05 ? 2 : data.Length + 3); // Length = Instruction + Data + Checksum

        // Instruction
        packet[4] = instruction;

        // Address and Data (REG WRITE only)
        if (instruction != 0x05)
        {
            packet[5] = address;
            Array.Copy(data, 0, packet, 6, data.Length);
        }

        // Calculate checksum
        byte checksum = CalculateChecksum(packet, 2);
        packet[packet.Length - 1] = checksum;

        return packet;
    }

    private byte[] ReadStatus(byte address, byte length)
    {
        clearBuffer();

        byte[] command = new byte[8];
        command[0] = 0xFF; // Header
        command[1] = 0xFF; // Header
        command[2] = (byte)ServoId; // Servo-ID
        command[3] = 0x04;
        command[4] = 0x02; // Instruction: READ DATA
        command[5] = address;
        command[6] = length;
        command[7] = CalculateChecksum(command, 2);

#if DEBUG
        Console.WriteLine("Sent package: " + BitConverter.ToString(command));
#endif

        serialPort.Write(command, 0, command.Length);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (serialPort.BytesToRead == 0)
        {
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                throw new TimeoutException("Timeout: Servo did not answer in time.");
            }
            Thread.Sleep(1);
        }

#if DEBUG
        Console.WriteLine($"Bytes in buffer: {serialPort.BytesToRead}");
#endif

        byte[] response = new byte[6 + length];
        int bytesRead = serialPort.Read(response, 0, response.Length);
        serialPort.DiscardInBuffer();

#if DEBUG
        Console.WriteLine($"Received answer: {BitConverter.ToString(response)}");
#endif

        byte checksum = CalculateChecksum(response, 2);
        if (response[response.Length - 1] != checksum)
        {
            throw new Exception("Checksum error.");
        }

        return response.Skip(5).Take(length).ToArray();
    }

    private byte CalculateChecksum(byte[] data, int startIndex)
    {
        int sum = 0;
        for (int i = startIndex; i < data.Length - 1; i++)
        {
            sum += data[i];
        }

        return (byte)(~sum & 0xFF);
    }

    private void clearBuffer()
    {
        // Empfangs- und Sende-Buffer leeren
        if (serialPort.BytesToRead > 0)
        {
            serialPort.DiscardInBuffer();
        }
        serialPort.DiscardOutBuffer();
    }
    #endregion

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Ressourcen freigeben
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                    serialPort.Dispose();
                }
            }
            disposed = true;
        }
    }

    ~ServoController()
    {
        Dispose(false);
    }
}
