using Gadgeteer.Modules.GHIElectronics;
using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GHI.Glide;
using Gadgeteer.Tool;
using System.IO;
using Microsoft.SPOT.IO;
using Skewworks.Labs;
using System.Text;
using System.Text.RegularExpressions;
//using IConnector;

namespace System.Diagnostics
{
    public enum DebuggerBrowsableState
    {
        Collapsed,
        Never,
        RootHidden
    }
}
namespace Gadgeteer.Shell
{
    #region Shell
    public class GvShell
    {
        #region Handler
        public delegate void IncomingPrintEventHandler(Bitmap result);
        public event IncomingPrintEventHandler PrintEvent;
        private void CallPrintEvent(Bitmap result)
        {
            // Event will be null if there are no subscribers
            if (PrintEvent != null)
            {
                PrintEvent(result);
            }
        }

        public delegate void IncomingClearScreenEventHandler();
        public event IncomingClearScreenEventHandler ClearScreenEvent;
        private void CallClearScreenEvent()
        {
            // Event will be null if there are no subscribers
            if (ClearScreenEvent != null)
            {
                ClearScreenEvent();
            }
        }
        #endregion Handler

        #region controller
        public DisplayTE35 displayTE35 { set; get; }
        public SDCard sdCard { set; get; }
        public USBClientEDP usbClientEDP { set; get; }
        public USBHost usbHost { set; get; }
        #endregion
        static SBASIC basic;
        public static string CurrentPath { set; get; }
        public static int CurrentLine { set; get; }
        public string TypedCommand { set; get; }
        public Bitmap Screen { set; get; }
        public int ScreenWidth { set; get; } = 320;
        const int FontHeight = 20;
        public Color ForeGround { set; get; } = GT.Color.White;
        public Color BackGround { set; get; } = GT.Color.Black;
        public ArrayList DataLines { set; get; }
        public int MaxLine { set; get; }
        public int ScreenHeight { set; get; } = 240;
        public Font CurrentFont { set; get; }
        private void ClearScreen()
        {
            Screen.Clear();
            Screen.DrawRectangle(Color.Black, 0, 0, 0, ScreenWidth, ScreenHeight, 0, 0, Color.Black, 0, 0, Color.Black, 0, 0, 100);
        }
        public void ExecuteScript(string Cmd)
        {
            string[] ParsedMsg = Cmd.Split(' ');
            PrintLine(">" + TypedCommand);
            switch (ParsedMsg[0].ToUpper())
            {
                case "CLS":
                    ClearScreen();
                    for (int i = 0; i < MaxLine; i++) DataLines[i] = string.Empty;
                    CurrentLine = 0;
                    break;
                case "DIR":
                    if (sdCard.IsCardInserted && sdCard.IsCardMounted)
                    {
                        ShowFiles();
                    }
                    break;
                case "CD..":
                    {
                        DirectoryInfo dir = new DirectoryInfo(CurrentPath);
                        if (CurrentPath.ToUpper() != "\\SD\\")
                        {
                            CurrentPath = Strings.NormalizeDirectory(dir.Parent.FullName);

                        }
                        PrintLine("Current:" + CurrentPath);
                    }
                    break;
                case "CD":
                    {
                        DirectoryInfo dir = new DirectoryInfo(CurrentPath + ParsedMsg[1]);
                        if (dir.Exists)
                        {
                            CurrentPath = Strings.NormalizeDirectory(dir.FullName);
                            PrintLine("Current:" + CurrentPath);
                        }
                    }
                    break;
                case "PRINT":
                    if (ParsedMsg.Length >= 2)
                    {
                        PrintFile(ParsedMsg[1]);
                    }
                    break;
                default:
                    bool Executed = false;
                    if (ParsedMsg.Length == 1)
                    {
                        //execute file
                        Executed = ExecuteFile(ParsedMsg[0]);
                    }
                    if (!Executed)
                        PrintLine("Unknown command.");
                    break;
            }
            PrintLine(">", false);
            CallPrintEvent(Screen);
        }

        bool PrintFile(string Filename)
        {
            bool Result = false;
            FileInfo info = new FileInfo(CurrentPath + Filename);
            if (info.Exists)
            {
                var data = sdCard.StorageDevice.ReadFile(info.FullName);
                var strdata = new string(Encoding.UTF8.GetChars(data));
                try
                {

                    foreach (var str in Regex.Split("\r\n", strdata,
                        RegexOptions.IgnoreCase))
                    {
                        PrintLine(str);
                        CallPrintEvent(Screen);
                    }
                    Result = true;
                }
                catch { }
            }
            return Result;
        }
        bool ExecuteFile(string Filename)
        {
            bool Result = false;
            FileInfo info = new FileInfo(CurrentPath + Filename);
            if (info.Exists)
            {
                switch (info.Extension.ToLower())
                {
                    case ".bas":
                        {
                            var data = sdCard.StorageDevice.ReadFile(info.FullName);
                            var codes = new string(Encoding.UTF8.GetChars(data));
                            try
                            {
                                basic.Run(codes);
                                Result = true;
                            }
                            catch { }
                        }
                        break;
                    case ".pe":
                        try
                        {
                            //DeviceController ctl = new DeviceController(displayTE35, sdCard, usbHost, usbClientEDP);
                            Launcher.ExecApp(info.FullName);
                            Result = true;
                        }
                        catch { }
                        break;
                    case ".jpg":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Jpeg);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);
                        }
                        break;
                    case ".bmp":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Bmp);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);
                        }
                        break;
                    case ".gif":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Gif);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);

                        }
                        break;
                }
            }
            return Result;
        }
        void ShowFiles()
        {
            if (VolumeInfo.GetVolumes()[0].IsFormatted)
            {
                if (Directory.Exists(CurrentPath))
                {
                    DirectoryInfo dir = new DirectoryInfo(CurrentPath);

                    var files = dir.GetFiles();
                    var folders = dir.GetDirectories();

                    PrintLine("Files available on " + CurrentPath + ":");
                    if (files.Length > 0)
                    {
                        for (int i = 0; i < files.Length; i++)
                            PrintLine(files[i].Name + " - " + Strings.FormatDiskSize(files[i].Length));
                    }
                    else
                    {
                        PrintLine("Files not found.");
                    }

                    PrintLine("Folders available on " + CurrentPath + ":");
                    if (folders.Length > 0)
                    {
                        for (int i = 0; i < folders.Length; i++)
                            PrintLine(folders[i].Name);
                    }
                    else
                    {
                        PrintLine("folders not found.");
                    }
                }
            }
            else
            {
                PrintLine("Storage is not formatted. " +
                    "Format on PC with FAT32/FAT16 first!");
            }
        }
        public void PrintLine(string Output, bool AddNewLine = true)
        {
            if (CurrentLine >= MaxLine - 1)
            {
                ClearScreen();
                for (int i = 0; i < MaxLine - 1; i++)
                {
                    DataLines[i] = DataLines[i + 1];
                    Screen.DrawText(DataLines[i].ToString(), CurrentFont, ForeGround, 5, FontHeight * i);
                }
                DataLines[CurrentLine] = Output;
                Screen.DrawText(Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
            }
            else
            {
                DataLines[CurrentLine] = Output;
                Screen.DrawText(Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
                if (AddNewLine)
                    CurrentLine++;
            }

        }
        public void PrintLine(int LineNumber, string Output)
        {
            ClearScreen();
            for (int i = 0; i <= CurrentLine - 1; i++)
            {
                Screen.DrawText(DataLines[i].ToString(), CurrentFont, ForeGround, 5, FontHeight * i);
            }
            Screen.DrawText(">" + Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
        }
        public void TypeInCommand(char KeyDown)
        {
            switch ((int)KeyDown)
            {
                //enter
                case 10:
                    ExecuteScript(TypedCommand.Trim());
                    TypedCommand = string.Empty;
                    break;
                //backspace
                case 8:
                    if (TypedCommand.Length > 0)
                    {
                        TypedCommand = TypedCommand.Substring(0, TypedCommand.Length - 1);
                        PrintLine(CurrentLine, TypedCommand);
                        CallPrintEvent(Screen);
                    }
                    break;
                default:
                    TypedCommand = TypedCommand + KeyDown.ToString();
                    PrintLine(CurrentLine, TypedCommand);
                    CallPrintEvent(Screen);
                    break;
            }

        }
        public GvShell(ref SDCard sdCard, ref USBHost usbHost, ref DisplayTE35 displayT35, ref USBClientEDP usbClientEdp)
        {
            this.displayTE35 = displayT35;
            this.usbHost = usbHost;
            this.usbClientEDP = usbClientEdp;
            this.sdCard = sdCard;
            Screen = new Bitmap(ScreenWidth, ScreenHeight);
            ClearScreen();
            MaxLine = ScreenHeight / 20;
            CurrentLine = 0;
            CurrentFont = Resources.GetFont(Resources.FontResources.NinaB);
            CurrentPath = "\\SD\\";
            DataLines = new ArrayList();
            for (int i = 0; i < MaxLine; i++) DataLines.Add(string.Empty);
            TypedCommand = string.Empty; if (basic == null)
                if (basic == null)
                {
                    basic = new SBASIC();
                    basic.Print += Basic_Print;
                    basic.ClearScreen += Basic_ClearScreen;
                }

        }

        public void PrintWelcome()
        {
            PrintLine("Welcome to Gadgeteer Shell (C) Gravicode");
            PrintLine(">", false);
            CallPrintEvent(Screen);


        }

        private void Basic_ClearScreen(SBASIC sender)
        {
            ClearScreen();
        }

        private void Basic_Print(SBASIC sender, string value)
        {
            PrintLine(value);
            CallPrintEvent(Screen);
        }
    }
    #endregion

    #region Forms
    public class Screen
    {
        public enum ScreenTypes { Splash = 0, Prompt };
        public delegate void GoToFormEventHandler(ScreenTypes form, params string[] Param);
        public event GoToFormEventHandler FormRequestEvent;
        protected void CallFormRequestEvent(ScreenTypes form, params string[] Param)
        {
            // Event will be null if there are no subscribers
            if (FormRequestEvent != null)
            {
                FormRequestEvent(form, Param);
            }
        }
        protected GHI.Glide.Display.Window MainWindow { set; get; }
        public virtual void Init(params string[] Param)
        {
            //do nothing
        }

        public Screen(ref GHI.Glide.Display.Window window)
        {
            MainWindow = window;
        }
    }
    public class PromptForm : Screen
    {
        int LineCounter
        {
            set; get;
        }

        const int LineSpacing = 20;
        private Gadgeteer.Modules.GHIElectronics.DisplayTE35 displayTE35;
        SDCard sdCard;
        USBHost usbHost;
        USBClientEDP usbClientEDP;
        GHI.Glide.UI.Image imgCode { set; get; }
        ArrayList LinesOfCode;

        public PromptForm(ref GHI.Glide.Display.Window window, ref Gadgeteer.Modules.GHIElectronics.DisplayTE35 displayTE35, ref SDCard sdCard, ref USBHost usbHost, ref USBClientEDP usbClientEDP) : base(ref window)
        {
            this.usbClientEDP = usbClientEDP;
            this.usbHost = usbHost;
            this.sdCard = sdCard;
            this.displayTE35 = displayTE35;
        }
        public override void Init(params string[] Param)
        {
            LinesOfCode = new ArrayList();
            LineCounter = 0;
            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.PromptForm));

            imgCode = (GHI.Glide.UI.Image)MainWindow.GetChildByName("imgCode");

            Glide.MainWindow = MainWindow;
            GvShell s = new GvShell(ref sdCard, ref usbHost, ref displayTE35, ref usbClientEDP);
            s.PrintEvent += S_Print;
            s.ClearScreenEvent += S_ClearScreen;

            usbHost.ConnectedKeyboard.KeyDown += (GHI.Usb.Host.Keyboard sender, GHI.Usb.Host.Keyboard.KeyboardEventArgs args) =>
            {
                Debug.Print(((int)args.ASCII).ToString());
                s.TypeInCommand(args.ASCII);
            };
            s.PrintWelcome();
            Thread.Sleep(500);

            //execute the code
            //s.ExecuteScript(Param[0]);
            //MainWindow.Invalidate();
        }



        private void S_ClearScreen()
        {

        }

        private void S_Print(Bitmap value)
        {
            imgCode.Bitmap = value;
            imgCode.Invalidate();
        }
    }
    public class SplashForm : Screen
    {
        public SplashForm(ref GHI.Glide.Display.Window window) : base(ref window)
        {

        }
        public override void Init(params string[] Param)
        {

            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.SplashForm));
            var img = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgLogo");

            GT.Picture pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.logo), GT.Picture.PictureEncoding.JPEG);
            img.Bitmap = pic.MakeBitmap();

            Glide.MainWindow = MainWindow;
            //MainWindow.Invalidate();
            Thread.Sleep(2000);
            CallFormRequestEvent(ScreenTypes.Prompt);

        }
    }

    #endregion
    public partial class Program
    {
        private static GHI.Glide.Display.Window MainWindow;
        private static Screen.ScreenTypes ActiveWindow { set; get; }
        Hashtable Screens { set; get; }
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            /*******************************************************************************************
            Modules added in the Program.gadgeteer designer view are used by typing 
            their name followed by a period, e.g.  button.  or  camera.
            
            Many modules generate useful events. Type +=<tab><tab> to add a handler to an event, e.g.:
                button.ButtonPressed +=<tab><tab>
            
            If you want to do something periodically, use a GT.Timer and handle its Tick event, e.g.:
                GT.Timer timer = new GT.Timer(1000); // every second (1000ms)
                timer.Tick +=<tab><tab>
                timer.Start();
            *******************************************************************************************/


            // Use Debug.Print to show messages in Visual Studio's "Output" window during debugging.
            Debug.Print("Program Started");
            Screens = new Hashtable();
            //populate all form
            var F1 = new SplashForm(ref MainWindow);
            F1.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Splash, F1);

            var F2 = new PromptForm(ref MainWindow, ref displayTE35, ref sdCard, ref usbHost, ref usbClientEDP);
            F2.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Prompt, F2);

            Glide.FitToScreen = true;
            GlideTouch.Initialize();

            //load splash
            LoadForm(Screen.ScreenTypes.Splash);


            //string dir = Strings.NormalizeDirectory("\\sd");
            //string siz = Strings.FormatDiskSize(1128);
        }
        void LoadForm(Screen.ScreenTypes form, params string[] Param)
        {
            ActiveWindow = form;
            switch (form)
            {
                case Screen.ScreenTypes.Splash:
                case Screen.ScreenTypes.Prompt:
                    (Screens[form] as Screen).Init(Param);
                    break;
                default:
                    return;
                    //throw new Exception("Belum diterapkan");
            }

        }
        void General_FormRequestEvent(Screen.ScreenTypes form, params string[] Param)
        {
            LoadForm(form, Param);
        }
        
    }
}
