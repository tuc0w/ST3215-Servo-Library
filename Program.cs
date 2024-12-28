using System;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            using (var servo = new ServoController("COM5"))
            {
                Console.WriteLine("Position: " + servo.ReadPos() + " steps");
                Console.WriteLine("Speed: " + servo.ReadSpeed() + " steps/s");
                Console.WriteLine("Load: " + servo.ReadLoad() + " %");
                Console.WriteLine("Voltage: " + servo.ReadVoltage() + " V");
                Console.WriteLine("Temperature: " + servo.ReadTemperature() + " °C");
                Console.WriteLine("Moving: " + (servo.IsMoving() ? "Yes" : "No"));
                Console.WriteLine("Current: " + servo.ReadCurrent() + " mA");

                Console.WriteLine("-------------- Starting Test --------------");
                for (int i = 0; i < 1; i++)
                {
                    Console.WriteLine(">>> Moving Servo to -90°");
                    servo.RotateToAngle(-90, 1024);
                    System.Threading.Thread.Sleep(5000);

                    Console.WriteLine(">>> Moving Servo to 0°");
                    servo.RotateToAngle(0, 1024);
                    System.Threading.Thread.Sleep(5000);

                    Console.WriteLine(">>> Moving Servo to 90°");
                    servo.RotateToAngle(90, 1024);
                    System.Threading.Thread.Sleep(5000);

                    Console.WriteLine(">>> Moving Servo to 0°");
                    servo.RotateToAngle(0, 1024);
                    System.Threading.Thread.Sleep(5000);
                }
                Console.WriteLine("-------------- Test done --------------");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
