using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace Hook
{
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    class Program
    {
        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO Dummy);//check fort user input

        public static uint GetIdleTime()
        {
            LASTINPUTINFO LastUserAction = new LASTINPUTINFO();
            LastUserAction.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(LastUserAction);
            GetLastInputInfo(ref LastUserAction);
            return ((uint)Environment.TickCount - LastUserAction.dwTime);
        }

        static bool UserWork = false; //flag for not to write each time and stuck in loop

        static void Main(string[] args)
        {


            int idleTimer = 60 * 1000; //How Much time to wait idle check
            string fileName; //FileName To Log 
            Console.WriteLine("Waiting for system idle...");

            DateTime StopTime = DateTime.Now;
            while (true)
            {
                Thread.Sleep(1000);
                DateTime time = DateTime.Now;
                string TimeWrite = time.ToString("dd-MM-yyyy HH:mm:ss"); //Set Time format to write on the file
                fileName = ".\\" + DateTime.Now.ToString("dd-MM-yyyy") + ".csv"; //FileName using dd-MM-YY.txt Exmp: 20-02-2021.csv
  //              fileName = ".\\" + DateTime.Now.AddDays(-1).ToString("dd-MM-yyyy");
                bool logfileExist = File.Exists(fileName);
                string myUserNamme, myAction, TimeToWrite, strRes;
                double BraekingTimeToLog;
                bool blnRemote;

                {
                    myUserNamme = Environment.GetEnvironmentVariable("Username");

                    if (GetIdleTime() >= idleTimer && !UserWork) //check if the stop user interacte with the system for idleTimer time
                    {
                        StopTime = DateTime.Now;
                        Console.WriteLine("{0} Not Working On Comp {1} Remotly: {2}", Environment.GetEnvironmentVariable("Username"), time, isRemote());
                        myAction = "Stop";
                        TimeToWrite = TimeWrite;
                        BraekingTimeToLog = 0;
                        blnRemote = isRemote();
                        strRes = "-";
                        AppendToMyFile(fileName, myUserNamme, myAction, TimeToWrite, BraekingTimeToLog, blnRemote, strRes);
                        UserWork = true;
                    }

                    if (GetIdleTime() < idleTimer && UserWork)
                    {
                        strRes = "OutOfOffice";
                        double breakingTime = (time - StopTime).TotalMinutes;
                        breakingTime = Math.Floor(breakingTime);

                        if (breakingTime >= 0 && breakingTime <= 5)
                        {
                            if (logfileExist)
                            {
                                var lines = File.ReadAllLines(fileName);
                                if (lines.Length >= 2)
                                {
                                    File.WriteAllLines(fileName, lines.Take(lines.Length - 1).ToArray());
                                    Console.WriteLine("delete last line of the file");
                                }
                            }
                        }
                        else if (!logfileExist)
                        {
                            myAction = "Start-Day";
                            TimeToWrite = TimeWrite;
                            BraekingTimeToLog = 0;
                            blnRemote = isRemote();
                            strRes = "-";
                            AppendToMyFile(fileName, myUserNamme, myAction, TimeToWrite, BraekingTimeToLog, blnRemote, strRes);
                            Console.Clear();
                            Console.WriteLine("Starting New Day...");
                        }
                        else
                        {
                            try
                            {

                                Console.WriteLine("Please Supply Where Have You Been, if no reason supply witthin 30 sec's the default is OutOfOffice?");
                                Console.WriteLine("Can Be 1/2/3./Any Other freestyle reason");
                                Console.WriteLine("Example: 3) Read mails offline");
                                Console.WriteLine("Example: 3. Read mails offline");
                                Console.WriteLine("1) Appoitment");
                                Console.WriteLine("2) Phone Call");
                                Console.WriteLine("3) freestyle reason, type it in your words");
                                strRes = Reader.ReadLine(30000);
                                switch (strRes)
                                {
                                    case "1":
                                        strRes = "Appoitment";
                                        break;
                                    case "2":
                                        strRes = "PhoneCall";
                                        break;
                                    case "3.":
                                        strRes = strRes.Replace("3.", "").Replace("3)", "");
                                        break;
                                    default:
                                        break;
                                }
                            }
                            catch (TimeoutException)
                            {
                                strRes = "OutOfOffice";
                                Console.WriteLine("Peaked Default");
                            }
                            myAction = "Start";
                            TimeToWrite = TimeWrite;
                            BraekingTimeToLog = breakingTime;
                            blnRemote = isRemote();
                            AppendToMyFile(fileName, myUserNamme, myAction, TimeToWrite, BraekingTimeToLog, blnRemote, strRes);
                        }
                        Console.WriteLine("{0} Start Working {1}, Breaking Time {2} Minutes Remotly: {3}, BreakingReasone: {4}", Environment.GetEnvironmentVariable("Username"), time, breakingTime, isRemote(), strRes);

                        UserWork = false;

                    }

                }

            }

        }
        static void AppendToMyFile(string myFileName, string myUserNamme, string myAction, string TimeToWrite, double BreakTime, bool blnRemote, string strReasone)
        {
            if (!File.Exists(myFileName)) { using (StreamWriter sw = File.CreateText(myFileName)) { sw.WriteLine("UserName,Action,Time,BreakTime,Remotly,BreakTimeReason"); sw.Close(); }; } //check if file exists if not create it
            File.AppendAllText(myFileName, myUserNamme + "," + myAction + "," + TimeToWrite + "," + BreakTime + "," + blnRemote + "," + strReasone + "\r\n");
        }
        static bool isRemote()
        {
            bool isRemote = false;
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == 3389)
                {
                    isRemote = true;
                    break;
                }
            }
            return isRemote;
        }

        static bool IsItSundayYet()
        {
            DateTime DayOfTheWeek = DateTime.Now;
            if ((DayOfTheWeek.DayOfWeek == DayOfWeek.Friday) || (DayOfTheWeek.DayOfWeek == DayOfWeek.Saturday)) { return false; }
            return true;
        }

        class Reader
        {
            private static Thread inputThread;
            private static AutoResetEvent getInput, gotInput;
            private static string input;

            static Reader()
            {
                getInput = new AutoResetEvent(false);
                gotInput = new AutoResetEvent(false);
                inputThread = new Thread(reader);
                inputThread.IsBackground = true;
                inputThread.Start();
            }

            private static void reader()
            {
                while (true)
                {
                    getInput.WaitOne();
                    input = Console.ReadLine();
                    gotInput.Set();
                }
            }

            // omit the parameter to read a line without a timeout
            public static string ReadLine(int timeOutMillisecs = Timeout.Infinite)
            {
                getInput.Set();
                bool success = gotInput.WaitOne(timeOutMillisecs);
                if (success)
                {
                    return input;
                }
                else
                {
                    throw new TimeoutException("User did not provide input within the timelimit.");
                }
            }
        }

    }
}
