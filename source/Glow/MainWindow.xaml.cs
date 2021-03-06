﻿/* ----------------------------------------------------------------------
Glow
Copyright (C) 2017, 2018 Matt McManis
http://github.com/MattMcManis/Glow
http://glowmpv.github.io
mattmcmanis@outlook.com

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.If not, see <http://www.gnu.org/licenses/>. 
---------------------------------------------------------------------- */
using System;
using System.IO;
using System.Windows;
using System.Configuration;
using Glow.Properties;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;
using System.Windows.Documents;
using System.Text;
using System.Windows.Controls;
using System.Drawing;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Net;

namespace Glow
{
    public partial class MainWindow : Window
    {
        // View Model
        public ViewModel vm = new ViewModel();

        // -------------------------
        // Version
        // -------------------------
        // Current Version
        public static Version currentVersion;
        // GitHub Latest Version
        public static Version latestVersion;
        // Beta, Stable
        public static string currentBuildPhase = "alpha";
        public static string latestBuildPhase;
        public static string[] splitVersionBuildPhase;

        public string TitleVersion
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }


        /// <summary>
        ///     MainWindow
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            this.MinWidth = 712;
            this.MinHeight = 400;

            // -------------------------
            // Set Current Version to Assembly Version
            // -------------------------
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string assemblyVersion = fvi.FileVersion;
            currentVersion = new Version(assemblyVersion);

            // -------------------------
            // Title + Version
            // -------------------------
            TitleVersion = "Glow ~ mpv Configurator (" + Convert.ToString(currentVersion) + "-" + currentBuildPhase + ")";

            // -----------------------------------------------------------------
            /// <summary>
            ///     Control Binding
            /// </summary>
            // -----------------------------------------------------------------
            DataContext = vm;

            // --------------------------------------------------
            // Load Saved Settings
            // --------------------------------------------------

            // -------------------------
            // Import Config INI
            // -------------------------
            // config.ini settings
            if (File.Exists(Paths.configINIFile))
            {
                ConfigureWindow.ImportConfig(this, vm);
            }
            // Defaults
            else
            {
                ConfigureWindow.LoadDefaults(this, vm);
            }

            // -------------------------
            // Window Position
            // -------------------------
            if (this.Top == 0 &&
                this.Left == 0)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // -------------------------
            // Load Theme
            // -------------------------
            try
            {
                //Configure.theme = vm.Theme_SelectedItem.Replace(" ", string.Empty);
                App.Current.Resources.MergedDictionaries.Clear();
                App.Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
                {
                    Source = new Uri("Theme" + vm.Theme_SelectedItem.Replace(" ", string.Empty) + ".xaml", UriKind.RelativeOrAbsolute)
                });
            }
            catch
            {
                MessageBox.Show("Could not load theme.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

            // --------------------------------------------------
            // Load Fonts
            // --------------------------------------------------
            foreach (FontFamily font in ViewModel.installedFonts.Families)
            {
                if (!string.IsNullOrEmpty(font.Name)) {
                    //ViewModel.fonts.Add(font.Name);
                    vm.Fonts_Items.Add(font.Name);
                }
            }

            // Add default to fonts list
            vm.Fonts_Items.Insert(0, "default");
            //ViewModel.fonts.Insert(0, "default");

            // --------------------------------------------------
            // Control Defaults
            // --------------------------------------------------
            // Tooltip Duration
            ToolTipService.ShowDurationProperty.OverrideMetadata(
                typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

            // Profile Preset
            //vm.Profiles_SelectedItem = "Default";
            //ViewModel.ProfileSelectedItem = "Default";
            // Font
            //ViewModel.FontSelectedItem = "default";

            // --------------------------------------------------
            // Custom Profiles
            // --------------------------------------------------
            // Load Custom INI's
            Profiles.GetCustomProfiles(vm);
        }

        /// <summary>
        ///    Window Loaded
        /// </summary>
        public void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // -------------------------
            // Create Profiles folder if missing
            // -------------------------
            //if (!Directory.Exists(vm.ProfilesPath_Text))
            //{
            //    // Yes/No Dialog Confirmation
            //    //
            //    MessageBoxResult resultExport = MessageBox.Show("Profiles Folder does not exist. Automatically create it?",
            //                                                    "Directory Not Found",
            //                                                    MessageBoxButton.YesNo,
            //                                                    MessageBoxImage.Information);
            //    switch (resultExport)
            //    {
            //        // Create
            //        case MessageBoxResult.Yes:
            //            try
            //            {
            //                Directory.CreateDirectory(vm.ProfilesPath_Text);
            //            }
            //            catch
            //            {
            //                MessageBox.Show("Could not create Profiles folder. May require Administrator privileges.",
            //                                "Error",
            //                                MessageBoxButton.OK,
            //                                MessageBoxImage.Error);
            //            }
            //            break;
            //        // Use Default
            //        case MessageBoxResult.No:
            //            break;
            //    }
            //}

            // -------------------------
            // Check for Available Updates
            // -------------------------
            Task.Factory.StartNew(() =>
            {
                UpdateAvailableCheck();
            });

            // -------------------------
            // Load Text Color Previews 
            // -------------------------
            PreviewOSDFontColor(this);
            PreviewOSDBorderColor(this);
            PreviewOSDShadowColor(this);
            PreviewSubtitlesFontColor(this);
            PreviewSubtitlesBorderColor(this);
            PreviewSubtitlesShadowColor(this);
        }

        /// <summary>
        ///     Check For Internet Connection
        /// </summary>
        [System.Runtime.InteropServices.DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);

        public static bool CheckForInternetConnection()
        {
            int desc;
            return InternetGetConnectedState(out desc, 0);
        }

        /// <summary>
        ///    Update Available Check
        /// </summary>
        public void UpdateAvailableCheck()
        {
            if (CheckForInternetConnection() == true)
            {
                if (vm.UpdateAutoCheck_IsChecked == true)
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    WebClient wc = new WebClient();
                    // UserAgent Header
                    wc.Headers.Add(HttpRequestHeader.UserAgent, "Glow ~ mpv Configurator (https://github.com/MattMcManis/Glow)" + " v" + MainWindow.currentVersion + "-" + MainWindow.currentBuildPhase + " Self-Update");
                    //wc.Headers.Add("Accept-Encoding", "gzip,deflate"); //error

                    wc.Proxy = null;

                    // -------------------------
                    // Parse GitHub .version file
                    // -------------------------
                    string parseLatestVersion = string.Empty;

                    try
                    {
                        parseLatestVersion = wc.DownloadString("https://raw.githubusercontent.com/MattMcManis/Glow/master/.version");
                    }
                    catch
                    {
                        return;
                    }

                    // -------------------------
                    // Split Version & Build Phase by dash
                    // -------------------------
                    if (!string.IsNullOrEmpty(parseLatestVersion)) //null check
                    {
                        try
                        {
                            // Split Version and Build Phase
                            splitVersionBuildPhase = Convert.ToString(parseLatestVersion).Split('-');

                            // Set Version Number
                            latestVersion = new Version(splitVersionBuildPhase[0]); //number
                            latestBuildPhase = splitVersionBuildPhase[1]; //alpha
                        }
                        catch
                        {
                            return;
                        }

                        // Check if Stellar is the Latest Version
                        // Update Available
                        if (latestVersion > currentVersion)
                        {
                            //updateAvailable = " ~ Update Available: " + "(" + Convert.ToString(latestVersion) + "-" + latestBuildPhase + ")";

                            Dispatcher.Invoke(new Action(delegate
                            {
                                TitleVersion = "Glow ~ mpv Configurator (" + Convert.ToString(currentVersion) + "-" + currentBuildPhase + ")"
                                            + " ~ Update Available: " + "(" + Convert.ToString(latestVersion) + "-" + latestBuildPhase + ")";
                            }));
                        }
                        // Update Not Available
                        else if (latestVersion <= currentVersion)
                        {
                            return;
                        }
                    }
                }
            }

            // Internet Connection Failed
            else
            {
                MessageBox.Show("Could not detect Internet Connection.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                return;
            }
        }


        /// <summary>
        ///    Open New Window
        /// </summary>
        //private Boolean IsWindowOpened = false;
        //public void OpenWindow(Window window)
        //{
        //    // Detect which screen we're on
        //    var allScreens = System.Windows.Forms.Screen.AllScreens.ToList();
        //    var thisScreen = allScreens.SingleOrDefault(s => this.Left >= s.WorkingArea.Left && this.Left < s.WorkingArea.Right);

        //    // Start Window
        //    window = new Window();

        //    // Keep Window on Top
        //    window.Owner = GetWindow(this);

        //    // Only allow 1 Window instance
        //    if (IsWindowOpened) return;
        //    window.ContentRendered += delegate { IsWindowOpened = true; };
        //    window.Closed += delegate { IsWindowOpened = false; };

        //    // Position Relative to MainWindow
        //    window.Left = Math.Max((this.Left + (this.Width - window.Width) / 2), thisScreen.WorkingArea.Left);
        //    window.Top = Math.Max((this.Top + (this.Height - window.Height) / 2), thisScreen.WorkingArea.Top);

        //    // Open Window
        //    window.ShowDialog();
        //}


        /// <summary>
        ///    Preview OSD Font Color
        /// </summary>
        public static void PreviewOSDFontColor(MainWindow mainwindow)
        {
            // Color
            if (mainwindow.tbxOSDFontColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxOSDFontColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.osdFontColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }

            // Transparent
            else
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#000000");
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(0, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.osdFontColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }

        }

        /// <summary>
        ///    Preview OSD Border Color
        /// </summary>
        public static void PreviewOSDBorderColor(MainWindow mainwindow)
        {
            if (mainwindow.tbxOSDBorderColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxOSDBorderColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.osdBorderColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }
        }

        /// <summary>
        ///    Preview OSD Shadow Color
        /// </summary>
        public static void PreviewOSDShadowColor(MainWindow mainwindow)
        {
            if (mainwindow.tbxOSDShadowColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxOSDShadowColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.osdShadowColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }
        }

        /// <summary>
        ///    Preview Subtitle Font Color
        /// </summary>
        public static void PreviewSubtitlesFontColor(MainWindow mainwindow)
        {
            if (mainwindow.tbxSubtitlesFontColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxSubtitlesFontColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.subtitlesFontColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }
        }

        /// <summary>
        ///    Preview Subtitle Border Color
        /// </summary>
        public static void PreviewSubtitlesBorderColor(MainWindow mainwindow)
        {
            if (mainwindow.tbxSubtitlesBorderColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxSubtitlesBorderColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.subtitlesBorderColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }
        }

        /// <summary>
        ///    Preview Subtitle Shadow Color
        /// </summary>
        public static void PreviewSubtitlesShadowColor(MainWindow mainwindow)
        {
            if (mainwindow.tbxSubtitlesShadowColor.Text.ToString().Length == 6)
            {
                try
                {
                    System.Drawing.Color color = ColorPickerWindow.ConvertHexToRGB("#" + mainwindow.tbxSubtitlesShadowColor.Text.ToString());
                    System.Windows.Media.Color mediaColor = System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
                    System.Windows.Media.Brush brushPreview = new System.Windows.Media.SolidColorBrush(mediaColor);
                    mainwindow.subtitlesShadowColorPreview.Fill = brushPreview;
                }
                catch
                {

                }
            }
        }


        /// <summary>
        ///     Close / Exit (Method)
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // Force Exit All Executables
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        void Window_Closing(object sender, CancelEventArgs e)
        {
            // -------------------------
            // Export Config INI
            // -------------------------
            // Overwrite only if changes made
            if (File.Exists(Paths.configINIFile))
            {
                ConfigureWindow.INIFile inif = new ConfigureWindow.INIFile(Paths.configINIFile);

                double? top = Convert.ToDouble(inif.Read("Main Window", "Window_Position_Top"));
                double? left = Convert.ToDouble(inif.Read("Main Window", "Window_Position_Left"));
                double? width = Convert.ToDouble(inif.Read("Main Window", "Window_Width"));
                double? height = Convert.ToDouble(inif.Read("Main Window", "Window_Height"));

                if (// Main Window
                    this.Top != top ||
                    this.Left != left ||
                    this.Width != width ||
                    this.Height != height ||
                    vm.mpvPath_Text != inif.Read("Main Window", "mpvPath_Text") ||
                    vm.mpvConfigPath_Text != inif.Read("Main Window", "mpvConfigPath_Text") ||
                    vm.ProfilesPath_Text != inif.Read("Main Window", "ProfilesPath_Text") ||
                    vm.Theme_SelectedItem != inif.Read("Configure Window", "Theme_SelectedItem") ||
                    vm.UpdateAutoCheck_IsChecked != Convert.ToBoolean(inif.Read("Configure Window", "UpdateAutoCheck_IsChecked").ToLower())
                    )
                {
                    ConfigureWindow.ExportConfig(this, vm);
                }
            }

            // Export Defaults & Currently Selected
            else if (!File.Exists(Paths.configINIFile))
            {
                ConfigureWindow.ExportConfig(this, vm);
            }

            // Exit
            e.Cancel = true;
            System.Windows.Forms.Application.ExitThread();
            Environment.Exit(0);
        }


        // --------------------------------------------------------------------------------------------------------
        /// <summary>
        ///    Methods
        /// </summary>
        // --------------------------------------------------------------------------------------------------------

        /// <summary>
        ///    Config RichTextBox
        /// </summary>
        public String ConfigRichTextBox()
        {
            // Select All Text
            TextRange textRange = new TextRange(
                rtbConfig.Document.ContentStart,
                rtbConfig.Document.ContentEnd
            );

            // Remove Formatting
            textRange.ClearAllProperties();

            // Return Text
            return textRange.Text;
        }


        public static void AllowOnlyAlphaNumeric(MainWindow window, System.Windows.Input.KeyEventArgs e)
        {
            // Escape closes window
            if (Key.Escape == e.Key)
            {
                window.Close();
            }

            // Disallow Symbols
            else if (Keyboard.IsKeyDown(Key.LeftShift) && e.Key >= Key.D0 && e.Key <= Key.D9 ||
                     Keyboard.IsKeyDown(Key.RightShift) && e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                e.Handled = true;
            }

            // Allow alphanumeric A-Z, 0-9
            // Ctrl Shortcuts
            // Backspace, Delete
            else if (e.Key >= Key.D0 && e.Key <= Key.D9 ||
                e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9 ||
                e.Key >= Key.A && e.Key <= Key.F ||
                Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.Z ||
                Keyboard.IsKeyDown(Key.RightCtrl) && e.Key == Key.Z ||
                Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.A ||
                Keyboard.IsKeyDown(Key.RightCtrl) && e.Key == Key.A ||
                Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.X ||
                Keyboard.IsKeyDown(Key.RightCtrl) && e.Key == Key.X ||
                Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.C ||
                Keyboard.IsKeyDown(Key.RightCtrl) && e.Key == Key.C ||
                Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.V ||
                Keyboard.IsKeyDown(Key.RightCtrl) && e.Key == Key.V ||
                e.Key == Key.Delete ||
                e.Key == Key.Back)
            {
                e.Handled = false;

            }

            // All other keys
            else
            {
                e.Handled = true;
            }

            // Tab pressed, focus must go to the next control
            if (e.Key == Key.Tab)
            {
                e.Handled = false;
            }
        }




        // --------------------------------------------------------------------------------------------------------
        /// <summary>
        ///    Controls
        /// </summary>
        // --------------------------------------------------------------------------------------------------------

        // --------------------------------------------------
        // General Controls
        // --------------------------------------------------

        /// <summary>
        ///     Geometry X
        /// </summary>
        private void slGeometryX_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slGeometryX.Value = 50;
        }

        /// <summary>
        ///     Geometry X
        /// </summary>
        private void slGeometryY_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slGeometryY.Value = 50;
        }

        /// <summary>
        ///     Autofit Width
        /// </summary>
        private void slAutofitWidth_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slAutofitWidth.Value = 100;
        }

        /// <summary>
        ///     Autofit Height
        /// </summary>
        private void slAutofitHeight_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slAutofitHeight.Value = 95;
        }

        /// <summary>
        ///     Log Label Reset
        /// </summary>
        private void lbLogPath_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            tbxLogPath.Text = "";
        }

        /// <summary>
        ///     Log Path
        /// </summary>
        private void tbxLogPath_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {

            // Open Folder Browser
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();

            // If OK
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                tbxLogPath.Text = folderBrowserDialog.SelectedPath.TrimEnd('\\') + @"\";
            }
        }

        /// <summary>
        ///     Screenshot Label Reset
        /// </summary>
        private void lbScreenshotPath_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            tbxScreenshotPath.Text = "";
        }

        /// <summary>
        ///     Screenshot Path
        /// </summary>
        private void tbxScreenshotPath_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Open Folder Browser
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();

            // If OK
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                tbxScreenshotPath.Text = folderBrowserDialog.SelectedPath.TrimEnd('\\') + @"\";
            }
        }

        /// <summary>
        ///     Screenshot Format
        /// </summary>
        private void cboScreenshotFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Change Label
            // Change Quality Slider Maximum

            // jpg
            if ((string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "jpg"
                || (string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "jpeg")
            {
                lbScreenshotQuality.Content = "Quality";
                slScreenshotQuality.Maximum = 100;
                slScreenshotQuality.Value = 95;
            }
                
            // png
            else if ((string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "png")
            {
                lbScreenshotQuality.Content = "Compression";
                slScreenshotQuality.Maximum = 9;
                slScreenshotQuality.Value = 7;
            }     

        }

        /// <summary>
        ///     Screenshot Quality DoubleClick
        /// </summary>
        private void slScreenshotQuality_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // jpg
            if ((string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "jpg"
                || (string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "jpeg")
            {
                // return to default
                slScreenshotQuality.Value = 95;
            }
                
            // png
            else if ((string)(cboScreenshotFormat.SelectedItem ?? string.Empty) == "png")
            {
                // return to default
                slScreenshotQuality.Value = 7;
            } 
        }

        // --------------------------------------------------
        // Video Controls
        // --------------------------------------------------

        /// <summary>
        ///    Video Driver
        /// </summary>
        private void cboVideoDriver_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable OpenGL PBO
            // Enable/Disable Scaling

            // Default
            if (vm.VideoDriver_SelectedItem == "default")
            {
                // Video Driver API
                vm.VideoDriverAPI_SelectedItem = "default";
                // Scaling On
                cboSigmoid.SelectedItem = "default";
                // Scale
                cboScale.SelectedItem = "default";
                // Chroma Scale
                cboChromaScale.SelectedItem = "default";
                // Downscale
                cboDownscale.SelectedItem = "default";
                // Interpolation Scale
                cboInterpolationScale.SelectedItem = "default";
            }

            // Enabled
            if (vm.VideoDriver_SelectedItem == "default" ||
                vm.VideoDriver_SelectedItem == "gpu" ||
                vm.VideoDriver_SelectedItem == "gpu-hq"
                //(string)(cboVideoDriver.SelectedItem ?? string.Empty) == "opengl" || // old
                //(string)(cboVideoDriver.SelectedItem ?? string.Empty) == "opengl-hq" // old
                //(string)(cboVideoDriver.SelectedItem ?? string.Empty) == "direct3d"
                //(string)(cboVideoDriver.SelectedItem ?? string.Empty) == "vaapi"
                //(string)(cboVideoDriver.SelectedItem ?? string.Empty) == "caca"
                )
            {
                // PBO On
                cboOpenGLPBO.IsEnabled = true;
                cboOpenGLPBO.SelectedItem = "default";

                // Scaling On
                cboSigmoid.IsEnabled = true;
                //cboSigmoid.SelectedItem = "default";
                // Scale
                cboScale.IsEnabled = true;
                //cboScale.SelectedItem = "default";
                slScaleAntiring.IsEnabled = true;
                tbxScaleAntiring.IsEnabled = true;
                // Chroma Scale
                cboChromaScale.IsEnabled = true;
                //cboChromaScale.SelectedItem = "default";
                slChromaAntiring.IsEnabled = true;
                tbxChromaAntiring.IsEnabled = true;
                // Downscale
                cboDownscale.IsEnabled = true;
                //cboDownscale.SelectedItem = "default";
                slDownscaleAntiring.IsEnabled = true;
                tbxDownscaleAntiring.IsEnabled = true;
                // Interpolation Scale
                cboInterpolationScale.IsEnabled = true;
                //cboInterpolationScale.SelectedItem = "default";
                slInterpolationAntiring.IsEnabled = true;
                tbxInterpolationAntiring.IsEnabled = true;
            }
            // Disabled
            else
            {
                // PBO Off
                cboOpenGLPBO.SelectedItem = "off";
                cboOpenGLPBO.IsEnabled = false;

                // Scaling Off
                cboSigmoid.IsEnabled = false;
                cboSigmoid.SelectedItem = "no";
                // Scale
                cboScale.IsEnabled = false;
                cboScale.SelectedItem = "off";
                slScaleAntiring.IsEnabled = false;
                tbxScaleAntiring.IsEnabled = false;
                // Chroma Scale
                cboChromaScale.IsEnabled = false;
                cboChromaScale.SelectedItem = "off";
                slChromaAntiring.IsEnabled = false;
                tbxChromaAntiring.IsEnabled = false;
                // Downscale
                cboDownscale.IsEnabled = false;
                cboDownscale.SelectedItem = "off";
                slDownscaleAntiring.IsEnabled = false;
                tbxDownscaleAntiring.IsEnabled = false;
                // Interpolation Scale
                cboInterpolationScale.IsEnabled = false;
                cboInterpolationScale.SelectedItem = "off";
                slInterpolationAntiring.IsEnabled = false;
                tbxInterpolationAntiring.IsEnabled = false;
            }
        }


        /// <summary>
        ///    Video Driver API
        /// </summary>
        private void cboVideoDriverAPI_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable OpenGL PBO
            // Enable/Disable Scaling

            // Enabled
            if (vm.VideoDriverAPI_SelectedItem == "default" ||
                vm.VideoDriver_SelectedItem == "gpu"
                //(string)(cboVideoDriverAPI.SelectedItem ?? string.Empty) == "opengl" // old
                //|| (string)(cboVideoDriverAPI.SelectedItem ?? string.Empty) == "vulkan"
                //|| (string)(cboVideoDriverAPI.SelectedItem ?? string.Empty) == "d3d11"
                )
            {
                // PBO On
                cboOpenGLPBO.IsEnabled = true;
                cboOpenGLPBO.SelectedItem = "default";

                // Scaling On
                cboSigmoid.IsEnabled = true;
                cboSigmoid.SelectedItem = "default";
                // Scale
                cboScale.IsEnabled = true;
                cboScale.SelectedItem = "default";
                slScaleAntiring.IsEnabled = true;
                tbxScaleAntiring.IsEnabled = true;
                // Chroma Scale
                cboChromaScale.IsEnabled = true;
                cboChromaScale.SelectedItem = "default";
                slChromaAntiring.IsEnabled = true;
                tbxChromaAntiring.IsEnabled = true;
                // Downscale
                cboDownscale.IsEnabled = true;
                cboDownscale.SelectedItem = "default";
                slDownscaleAntiring.IsEnabled = true;
                tbxDownscaleAntiring.IsEnabled = true;
                // Interpolation Scale
                cboInterpolationScale.IsEnabled = true;
                cboInterpolationScale.SelectedItem = "default";
                slInterpolationAntiring.IsEnabled = true;
                tbxInterpolationAntiring.IsEnabled = true;

            }
            // Disabled
            else
            {
                // PBO Off
                cboOpenGLPBO.SelectedItem = "off";
                cboOpenGLPBO.IsEnabled = false;

                // Scaling Off
                cboSigmoid.IsEnabled = false;
                cboSigmoid.SelectedItem = "no";
                // Scale
                cboScale.IsEnabled = false;
                cboScale.SelectedItem = "off";
                slScaleAntiring.IsEnabled = false;
                tbxScaleAntiring.IsEnabled = false;
                // Chroma Scale
                cboChromaScale.IsEnabled = false;
                cboChromaScale.SelectedItem = "off";
                slChromaAntiring.IsEnabled = false;
                tbxChromaAntiring.IsEnabled = false;
                // Downscale
                cboDownscale.IsEnabled = false;
                cboDownscale.SelectedItem = "off";
                slDownscaleAntiring.IsEnabled = false;
                tbxDownscaleAntiring.IsEnabled = false;
                // Interpolation Scale
                cboInterpolationScale.IsEnabled = false;
                cboInterpolationScale.SelectedItem = "off";
                slInterpolationAntiring.IsEnabled = false;
                tbxInterpolationAntiring.IsEnabled = false;
            }
        }

        /// <summary>
        ///    OpenGL PBO
        /// </summary>
        private void cboOpenGLPBO_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable / Disable PBO Formats

            // Enabled
            if ((string)(cboOpenGLPBO.SelectedItem ?? string.Empty) == "yes")
            {
                cboOpenGLPBOFormat.IsEnabled = true;
                cboOpenGLPBOFormat.SelectedItem = "default";
            }
            // Disabled
            else
            {
                cboOpenGLPBOFormat.SelectedItem = "off";
                cboOpenGLPBOFormat.IsEnabled = false;
            }
        }

        /// <summary>
        ///     Interpolation
        /// </summary>
        private void cboInterpolation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((string)(cboInterpolation.SelectedItem ?? string.Empty) == "yes")
                cboVideoSync.SelectedItem = "display-resample";
            else
                cboVideoSync.SelectedItem = "default";
        }

        /// <summary>
        ///    Gamma Auto
        /// </summary>
        //private void cboGammaAuto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    // Enable/Disable Gamma

        //    // Enabled
        //    if ((string)(cboGammaAuto.SelectedItem ?? string.Empty) == "yes")
        //    {
        //        // Slider
        //        slGamma.Value = 0;
        //        slGamma.IsEnabled = false;
        //        // TextBox
        //        tbxGamma.IsEnabled = false;
        //    }
        //    // Disabled
        //    else if ((string)(cboGammaAuto.SelectedItem ?? string.Empty) == "no")
        //    {
        //        // Slider
        //        slGamma.IsEnabled = true;
        //        // TextBox
        //        tbxGamma.IsEnabled = true;
        //    }
        //}

        /// <summary>
        ///     ICC Profile Path
        /// </summary>
        private void lbICCProfilePath_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear
            //tbxICCProfilePath.Text = "";

            if (cboICCProfile.IsEditable == true)
                cboICCProfile.Text = "";
        }
        private void cboICCProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Custom ComboBox Editable
            if ((string)cboICCProfile.SelectedItem == "select")
            {
                cboICCProfile.IsEditable = true;

                // Clear Text
                //cboICCProfile.SelectedIndex = -1;

                // Open 'Select File'
                Microsoft.Win32.OpenFileDialog selectFile = new Microsoft.Win32.OpenFileDialog();
                selectFile.RestoreDirectory = true;
                // Show save file dialog box
                Nullable<bool> result = selectFile.ShowDialog();

                // Process dialog box
                if (result == true)
                {
                    cboICCProfile.Items.Add(selectFile.FileName);
                    cboICCProfile.SelectedItem = selectFile.FileName;
                    cboICCProfile.IsEditable = true;
                }
            }

            // Other Items Disable Editable
            else if ((string)cboICCProfile.SelectedItem != "select"
                && !string.IsNullOrEmpty((string)cboICCProfile.SelectedItem))
            {
                //cboICCProfile.Items[cboICCProfile.SelectedIndex] = "select";
                cboICCProfile.IsEditable = false;
            }
        }
        //private void tbxICCProfilePath_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    // Open 'Select File'
        //    Microsoft.Win32.OpenFileDialog selectFile = new Microsoft.Win32.OpenFileDialog();

        //    selectFile.RestoreDirectory = true;

        //    // Show save file dialog box
        //    Nullable<bool> result = selectFile.ShowDialog();

        //    // Process dialog box
        //    if (result == true)
        //    {
        //        tbxICCProfilePath.Text = selectFile.FileName;
        //    }
        //}

        /// <summary>
        ///    Deband
        /// </summary>
        private void cboDeband_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable Deband Grain

            // Enabled
            if ((string)(cboDeband.SelectedItem ?? string.Empty) == "yes")
            {
                tbxDebandGrain.IsEnabled = true;
            }
            // Disabled
            else if ((string)(cboDeband.SelectedItem ?? string.Empty) == "no")
            {
                tbxDebandGrain.IsEnabled = false;
                tbxDebandGrain.Text = "";
            }
            else if ((string)(cboDeband.SelectedItem ?? string.Empty) == "default")
            {
                tbxDebandGrain.IsEnabled = false;
                tbxDebandGrain.Text = "";
            }
        }

        /// <summary>
        ///    Scale
        /// </summary>
        private void cboScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable Scale Antiring

            // Off
            // Turn off Scale Antiring
            if ((string)(cboScale.SelectedItem ?? string.Empty) == "off")
            {
                slScaleAntiring.IsEnabled = false;
                slScaleAntiring.Value = 0;
                tbxScaleAntiring.IsEnabled = false;
            }
            // On
            // Enable Scale Antiring
            else
            {
                slScaleAntiring.IsEnabled = true;
                tbxScaleAntiring.IsEnabled = true;
            }
        }

        /// <summary>
        ///    Chroma Scale
        /// </summary>
        private void cboChromaScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable Chroma Scale Antiring

            // Off
            // Turn off Chroma Scale Antiring
            if ((string)(cboChromaScale.SelectedItem ?? string.Empty) == "off")
            {
                slChromaAntiring.IsEnabled = false;
                slChromaAntiring.Value = 0;
                tbxChromaAntiring.IsEnabled = false;
            }
            // On
            // Enable Chroma Scale Antiring
            else
            {
                slChromaAntiring.IsEnabled = true;
                tbxChromaAntiring.IsEnabled = true;
            }
        }

        /// <summary>
        ///    Downscale
        /// </summary>
        private void cboDownscale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable Downscale Antiring

            // Off
            // Turn off Downscale Antiring
            if ((string)(cboDownscale.SelectedItem ?? string.Empty) == "off")
            {
                slDownscaleAntiring.IsEnabled = false;
                slDownscaleAntiring.Value = 0;
                tbxDownscaleAntiring.IsEnabled = false;
            }
            // On
            // Enable Downscale Antiring
            else
            {
                slDownscaleAntiring.IsEnabled = true;
                tbxDownscaleAntiring.IsEnabled = true;
            }
        }

        /// <summary>
        ///    Software Scaler
        /// </summary>
        private void cboSoftwareScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable/Disable Hardware Scaling if Software Scaling is on

            // Default (Enabled)
            if ((string)(cboSoftwareScaler.SelectedItem ?? string.Empty) == "default")
            {
                // Sigmoid
                cboSigmoid.IsEnabled = true;
                cboSigmoid.SelectedItem = "default";

                // Scale
                cboScale.IsEnabled = true;
                slScaleAntiring.IsEnabled = true;
                tbxScaleAntiring.IsEnabled = true;

                // Chroma
                cboChromaScale.IsEnabled = true;
                slChromaAntiring.IsEnabled = true;
                tbxChromaAntiring.IsEnabled = true;

                // Downscale
                cboDownscale.IsEnabled = true;
                slDownscaleAntiring.IsEnabled = true;
                tbxDownscaleAntiring.IsEnabled = true;
            }
            // Enabled
            else if ((string)(cboSoftwareScaler.SelectedItem ?? string.Empty) == "off")
            {
                // Sigmoid
                cboSigmoid.IsEnabled = true;
                //cboSigmoid.SelectedItem = "default";

                // Scale
                cboScale.IsEnabled = true;
                slScaleAntiring.IsEnabled = true;
                tbxScaleAntiring.IsEnabled = true;

                // Chroma
                cboChromaScale.IsEnabled = true;
                slChromaAntiring.IsEnabled = true;
                tbxChromaAntiring.IsEnabled = true;

                // Downscale
                cboDownscale.IsEnabled = true;
                slDownscaleAntiring.IsEnabled = true;
                tbxDownscaleAntiring.IsEnabled = true;
            }
            // Disabled
            else
            {
                // Sigmoid
                cboSigmoid.IsEnabled = false;
                cboSigmoid.SelectedItem = "no";

                // Scale
                cboScale.IsEnabled = false;
                slScaleAntiring.IsEnabled = false;
                slScaleAntiring.Value = 0;
                tbxScaleAntiring.IsEnabled = false;

                // Chroma
                cboChromaScale.IsEnabled = false;
                slChromaAntiring.IsEnabled = false;
                slChromaAntiring.Value = 0;
                tbxChromaAntiring.IsEnabled = false;

                // Downscale
                cboDownscale.IsEnabled = false;
                slDownscaleAntiring.IsEnabled = false;
                slDownscaleAntiring.Value = 0;
                tbxDownscaleAntiring.IsEnabled = false;
            }

            //if ((string)(cboSoftwareScaler.SelectedItem ?? string.Empty) != "default")
            //{

            //}
        }

        /// <summary>
        ///     Video Brightness DoubleClick
        /// </summary>
        private void slBrightness_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slBrightness.Value = 0;
        }

        /// <summary>
        ///     Video Contrast DoubleClick
        /// </summary>
        private void slContrast_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slContrast.Value = 0;
        }

        /// <summary>
        ///     Video Hue DoubleClick
        /// </summary>
        private void slHue_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slHue.Value = 0;
        }

        /// <summary>
        ///     Video Saturation DoubleClick
        /// </summary>
        private void slSaturation_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slSaturation.Value = 0;
        }

        /// <summary>
        ///     Video Gamma DoubleClick
        /// </summary>
        private void slGamma_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slGamma.Value = 0;
        }

        /// <summary>
        ///     Video Scale Antiring
        /// </summary>
        private void slScaleAntiring_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slScaleAntiring.Value = 0;
        }

        /// <summary>
        ///     Video Chroma Antiring
        /// </summary>
        private void slChromaAntiring_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slChromaAntiring.Value = 0;
        }

        /// <summary>
        ///     Video Downscale Antiring
        /// </summary>
        private void cboDownscaleAntiring_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slDownscaleAntiring.Value = 0;
        }

        /// <summary>
        ///     Video Interpolation Antiring
        /// </summary>
        private void slInterpolationAntiring_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // return to default
            slInterpolationAntiring.Value = 0;
        }

        // --------------------------------------------------
        // Audio Controls
        // --------------------------------------------------

        /// <summary>
        ///     Volume Slider DoubleClick
        /// </summary>
        private void slVolume_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slVolume.Value = 100;
        }

        /// <summary>
        ///     Volume Max Slider DoubleClick
        /// </summary>
        private void slVolumeMax_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // return to default
            slVolumeMax.Value = 150;
        }

        /// <summary>
        ///     Soft Volume Max Slider DoubleClick
        /// </summary>
        //private void slSoftVolumeMax_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        //{
        //    // return to default
        //    slSoftVolumeMax.Value = 150;
        //}

        /// <summary>
        ///    Audio Languages
        /// </summary>
        private void listViewAudioLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //// Remove
            //foreach (string item in e.RemovedItems)
            //{
            //    Audio.listAudioLanguages.Remove(item);
            //    Audio.listAudioLanguages.TrimExcess();
            //}

            //// Add
            //foreach (string item in e.AddedItems)
            //{
            //    Audio.listAudioLanguages.Add(item);
            //}
        }

        /// <summary>
        ///    Audio Language Up
        /// </summary>
        private void buttonAudioLanguageUp_Click(object sender, RoutedEventArgs e)
        {
            if (listViewAudioLanguages.SelectedItems.Count > 0)
            {
                var selectedIndex = this.listViewAudioLanguages.SelectedIndex;

                if (selectedIndex > 0)
                {
                    var itemToMoveUp = ViewModel.AudioLanguageItems[selectedIndex];
                    ViewModel.AudioLanguageItems.RemoveAt(selectedIndex);
                    ViewModel.AudioLanguageItems.Insert(selectedIndex - 1, itemToMoveUp);
                    this.listViewAudioLanguages.SelectedIndex = selectedIndex - 1;
                }
            }
        }
        /// <summary>
        ///    Audio Language Down
        /// </summary>
        private void buttonAudioLanguageDown_Click(object sender, RoutedEventArgs e)
        {
            if (listViewAudioLanguages.SelectedItems.Count > 0)
            {
                var selectedIndex = this.listViewAudioLanguages.SelectedIndex;

                if (selectedIndex + 1 < ViewModel.AudioLanguageItems.Count)
                {
                    var itemToMoveDown = ViewModel.AudioLanguageItems[selectedIndex];
                    ViewModel.AudioLanguageItems.RemoveAt(selectedIndex);
                    ViewModel.AudioLanguageItems.Insert(selectedIndex + 1, itemToMoveDown);
                    this.listViewAudioLanguages.SelectedIndex = selectedIndex + 1;
                }
            }
        }
        /// <summary>
        ///    Audio Select All
        /// </summary>
        private void buttonAudioLanguageSelectAll_Click(object sender, RoutedEventArgs e)
        {
            listViewAudioLanguages.SelectAll();
        }
        /// <summary>
        ///    Audio Deselect All
        /// </summary>
        private void buttonAudioLanguageDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            listViewAudioLanguages.SelectedIndex = -1;
        }



        // --------------------------------------------------
        // Subtitle Controls
        // --------------------------------------------------

        /// <summary>
        ///     Subtitles
        /// </summary>
        private void cboSubtitles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Embedded Fonts Enabled
            if ((string)(cboSubtitles.SelectedItem ?? string.Empty) == "yes")
                // Disable Custom Font
                cboSubtitlesLoadFiles.SelectedItem = "fuzzy";
            else if ((string)(cboSubtitles.SelectedItem ?? string.Empty) == "no")
                cboSubtitlesLoadFiles.SelectedItem = "no";
            else
                cboSubtitlesLoadFiles.SelectedItem = "default";
        }

        /// <summary>
        ///    Subtitle Embedded Fonts
        /// </summary>
        private void cboSubtitlesEmbeddedFonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Embedded Fonts Enabled
            if ((string)(cboSubtitlesEmbeddedFonts.SelectedItem ?? string.Empty) == "yes")
                // Disable Custom Font
                cboSubtitlesFont.IsEnabled = false;

            // Embedded Fonts Disabled
            else if ((string)(cboSubtitlesEmbeddedFonts.SelectedItem ?? string.Empty) == "no")
                // Enable Custom Font
                cboSubtitlesFont.IsEnabled = true;
        }


        /// <summary>
        ///     Subtitle Font Color Button
        /// </summary>
        private void btnSubtitleFontColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("subtitlesFont");
        }
        /// <summary>
        ///     Subtitles Font Color TextBox
        /// </summary>
        private void tbxSubtitlesFontColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewSubtitlesFontColor(this);
        }
        private void tbxSubtitlesFontColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxSubtitlesFontColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        

        /// <summary>
        ///    Subtitle Border Color Button
        /// </summary>
        private void btnSubtitleBorderColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("subtitlesBorder");
        }
        /// <summary>
        ///     Subtitles Border Color TextBox
        /// </summary>
        private void tbxSubtitlesBorderColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewSubtitlesBorderColor(this);
        }
        private void tbxSubtitlesBorderColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxSubtitlesBorderColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }


        /// <summary>
        ///    Subtitle Shadow Color Button
        /// </summary>
        private void btnSubtitleShadowColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("subtitlesShadow");
        }
        /// <summary>
        ///     Subtitles Shadow Color TextBox
        /// </summary>
        private void tbxSubtitlesShadowColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewSubtitlesShadowColor(this);
        }
        private void tbxSubtitlesShadowColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxSubtitlesShadowColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }


        /// <summary>
        ///    Subtitle Shadow Color
        /// </summary>
        //private void cboSubtitleShadowColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    // Get Selected Item
        //    ComboBoxItem selectedItem = (ComboBoxItem)(cboSubtitlesShadowColor.SelectedValue);
        //    string selected = (string)(selectedItem.Content);

        //    // Disable
        //    if (selected == "None")
        //    {
        //        // slider
        //        slSubtitlesShadowOffset.IsEnabled = false;
        //        // textbox
        //        tbxSubtitlesShadowOffset.IsEnabled = false;
        //        tbxSubtitlesShadowOffset.Text = "0.00";
        //    }
        //    // Enable
        //    else
        //    {
        //        // slider
        //        slSubtitlesShadowOffset.IsEnabled = true;
        //        // textbox
        //        tbxSubtitlesShadowOffset.IsEnabled = true;
        //    }
        //}

        /// <summary>
        ///     Subtitle Position DoubleClick
        /// </summary>
        private void slSubtitlePosition_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slSubtitlePosition.Value = 95;
        }

        /// <summary>
        ///     Subtitle Shadow Offset DoubleClick
        /// </summary>
        private void slSubtitlesShadowOffset_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slSubtitlesShadowOffset.Value = 1.25;
        }

        /// <summary>
        ///    Subtitle Languages
        /// </summary>
        private void listViewSubtitleLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        /// <summary>
        ///    Subtitle Language Up
        /// </summary>
        private void buttonSubtitleLanguageUp_Click(object sender, RoutedEventArgs e)
        {
            if (listViewSubtitlesLanguages.SelectedItems.Count > 0)
            {
                var selectedIndex = this.listViewSubtitlesLanguages.SelectedIndex;

                if (selectedIndex > 0)
                {
                    var itemToMoveUp = ViewModel.SubtitlesLanguageItems[selectedIndex];
                    ViewModel.SubtitlesLanguageItems.RemoveAt(selectedIndex);
                    ViewModel.SubtitlesLanguageItems.Insert(selectedIndex - 1, itemToMoveUp);
                    this.listViewSubtitlesLanguages.SelectedIndex = selectedIndex - 1;
                }
            }
        }
        /// <summary>
        ///    Subtitle Language Down
        /// </summary>
        private void buttonSubtitleLanguageDown_Click(object sender, RoutedEventArgs e)
        {
            if (listViewSubtitlesLanguages.SelectedItems.Count > 0)
            {
                var selectedIndex = this.listViewSubtitlesLanguages.SelectedIndex;

                if (selectedIndex + 1 < ViewModel.SubtitlesLanguageItems.Count)
                {
                    var itemToMoveDown = ViewModel.SubtitlesLanguageItems[selectedIndex];
                    ViewModel.SubtitlesLanguageItems.RemoveAt(selectedIndex);
                    ViewModel.SubtitlesLanguageItems.Insert(selectedIndex + 1, itemToMoveDown);
                    this.listViewSubtitlesLanguages.SelectedIndex = selectedIndex + 1;
                }
            }
        }
        /// <summary>
        ///    Subtitle Select All
        /// </summary>
        private void buttonSubtitleLanguageSelectAll_Click(object sender, RoutedEventArgs e)
        {
            listViewSubtitlesLanguages.SelectAll();
        }
        /// <summary>
        ///    Subtitle Deselect All
        /// </summary>
        private void buttonSubtitleLanguageDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            listViewSubtitlesLanguages.SelectedIndex = -1;
        }



        // --------------------------------------------------
        // OSD Controls
        // --------------------------------------------------
        /// <summary>
        ///     OSD
        /// </summary>
        private void cboOSD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Disabling OSD disables its options           
            if ((string)(cboOSD.SelectedItem ?? string.Empty) == "no")
            {
                // Fractions
                cboOSDFractions.SelectedItem = "default";
                // Duration
                tbxOSDDuration.Text = "";
                // Level
                cboOSDLevel.SelectedItem = "default";
            }
        }

        /// <summary>
        ///     OSD Fractions
        /// </summary>
        private void cboOSDFractions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enables OSD
            if ((string)(cboOSDFractions.SelectedItem ?? string.Empty) == "yes")
            {
                cboOSD.SelectedItem = "yes";
            }
        }

        /// <summary>
        ///     OSD Duration
        /// </summary>
        private void tbxOSDDuration_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enables OSD
            if (tbxOSDDuration.Text != string.Empty)
            {
                cboOSD.SelectedItem = "yes";
            }
        }

        /// <summary>
        ///     OSD Level
        /// </summary>
        private void cboOSDLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enables OSD
            if ((string)(cboOSDLevel.SelectedItem ?? string.Empty) != "default")
            {
                cboOSD.SelectedItem = "yes";
            }
        }

        /// <summary>
        ///    OSD Font Button
        /// </summary>
        private void btnOSDFontColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("osdFont");
        }

        /// <summary>
        ///     OSD Font Color TextBox
        /// </summary>
        private void tbxOSDFontColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewOSDFontColor(this);
        }
        private void tbxOSDFontColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxOSDFontColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }


        /// <summary>
        ///    OSD Border Button
        /// </summary>
        private void btnOSDBorderColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("osdBorder");
        }

        /// <summary>
        ///     OSD Font Color TextBox
        /// </summary>
        private void tbxOSDBorderColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewOSDBorderColor(this);
        }
        private void tbxOSDBorderColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxOSDBorderColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }


        /// <summary>
        ///    OSD Shadow Button
        /// </summary>
        private void btnOSDShadowColor_Click(object sender, RoutedEventArgs e)
        {
            OpenColorPickerWindow("osdShadow");
        }

        /// <summary>
        ///     OSD Shadow Color TextBox
        /// </summary>
        private void tbxOSDShadowColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            PreviewOSDShadowColor(this);
        }
        private void tbxOSDShadowColor_KeyDown(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }
        private void tbxOSDShadowColor_KeyUp(object sender, KeyEventArgs e)
        {
            AllowOnlyAlphaNumeric(this, e);
        }

        /// <summary>
        ///    OSD Shadow
        /// </summary>
        //private void cboOSDFontShadowColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    // Get Selected Item
        //    ComboBoxItem selectedItem = (ComboBoxItem)(cboOSDShadowColor.SelectedValue);
        //    string selected = (string)(selectedItem.Content);

        //    // Disable
        //    if (selected == "None")
        //    {
        //        // slider
        //        slOSDShadowOffset.IsEnabled = false;
        //        tbxOSDShadowOffset.IsEnabled = false;
        //        // textbox
        //        tbxOSDShadowOffset.Text = "0.00";
        //    }
        //    // Enable
        //    else
        //    {
        //        // slider
        //        slOSDShadowOffset.IsEnabled = true;
        //        // textbox
        //        tbxOSDShadowOffset.IsEnabled = true;
        //    }

        //}

        /// <summary>
        ///     OSD Scale DoubleClick
        /// </summary>
        private void slOSDScale_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slOSDScale.Value = 0.5;
        }

        /// <summary>
        ///     OSD Bar Width DoubleClick
        /// </summary>
        private void slOSDBarWidth_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slOSDBarWidth.Value = 95;
        }

        /// <summary>
        ///     OSD Bar Height DoubleClick
        /// </summary>
        private void slOSDBarHeight_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slOSDBarHeight.Value = 2;
        }

        /// <summary>
        ///     OSD Shadow Offset DoubleClick
        /// </summary>
        private void slOSDShadowOffset_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            slOSDShadowOffset.Value = 1.25;
        }



        // --------------------------------------------------
        // Main Controls
        // --------------------------------------------------

        /// <summary>
        ///     Info Button
        /// </summary>
        private Boolean IsInfoWindowOpened = false;
        private void buttonInfo_Click(object sender, RoutedEventArgs e)
        {
            // Detect which screen we're on
            var allScreens = System.Windows.Forms.Screen.AllScreens.ToList();
            var thisScreen = allScreens.SingleOrDefault(s => this.Left >= s.WorkingArea.Left && this.Left < s.WorkingArea.Right);

            // Start Window
            InfoWindow infowindow = new InfoWindow();

            // Keep Window on Top
            infowindow.Owner = GetWindow(this);

            // Only allow 1 Window instance
            if (IsInfoWindowOpened) return;
            infowindow.ContentRendered += delegate { IsInfoWindowOpened = true; };
            infowindow.Closed += delegate { IsInfoWindowOpened = false; };

            // Position Relative to MainWindow
            infowindow.Left = Math.Max((this.Left + (this.Width - infowindow.Width) / 2), thisScreen.WorkingArea.Left);
            infowindow.Top = Math.Max((this.Top + (this.Height - infowindow.Height) / 2), thisScreen.WorkingArea.Top);

            // Open Window
            infowindow.Show();
        }

        /// <summary>
        ///    Website Button
        /// </summary>
        private void buttonWebsite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://glowmpv.github.io");
        }

        /// <summary>
        ///    Configure Window Button
        /// </summary>
        private void buttonConfigure_Click(object sender, RoutedEventArgs e)
        {
            // Detect which screen we're on
            var allScreens = System.Windows.Forms.Screen.AllScreens.ToList();
            var thisScreen = allScreens.SingleOrDefault(s => this.Left >= s.WorkingArea.Left && this.Left < s.WorkingArea.Right);

            // Start Window
            ConfigureWindow settingswindow = new ConfigureWindow(this, vm);

            // Keep Window on Top
            settingswindow.Owner = GetWindow(this);

            // Only allow 1 Window instance
            if (IsInfoWindowOpened) return;
            settingswindow.ContentRendered += delegate { IsInfoWindowOpened = true; };
            settingswindow.Closed += delegate { IsInfoWindowOpened = false; };

            // Position Relative to MainWindow
            settingswindow.Left = Math.Max((this.Left + (this.Width - settingswindow.Width) / 2), thisScreen.WorkingArea.Left);
            settingswindow.Top = Math.Max((this.Top + (this.Height - settingswindow.Height) / 2), thisScreen.WorkingArea.Top);

            // Open Window
            settingswindow.ShowDialog();
        }

        /// <summary>
        ///    Update Button
        /// </summary>
        private Boolean IsUpdateWindowOpened = false;
        private void buttonUpdate_Click(object sender, RoutedEventArgs e)
        {
            // Proceed if Internet Connection
            //
            if (UpdateWindow.CheckForInternetConnection() == true)
            {
                // Parse GitHub .version file
                //
                string parseLatestVersion = string.Empty;

                try
                {
                    parseLatestVersion = UpdateWindow.wc.DownloadString("https://raw.githubusercontent.com/MattMcManis/Glow/master/.version");
                }
                catch
                {
                    MessageBox.Show("GitHub version file not found.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);

                    return;
                }


                //Split Version & Build Phase by dash
                //
                if (!string.IsNullOrEmpty(parseLatestVersion)) //null check
                {
                    try
                    {
                        // Split Version and Build Phase
                        splitVersionBuildPhase = Convert.ToString(parseLatestVersion).Split('-');

                        // Set Version Number
                        latestVersion = new Version(splitVersionBuildPhase[0]); //number
                        latestBuildPhase = splitVersionBuildPhase[1]; //alpha
                    }
                    catch
                    {
                        MessageBox.Show("Error reading version.",
                                       "Error",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);

                        return;
                    }

                    // Debug
                    //MessageBox.Show(Convert.ToString(latestVersion));
                    //MessageBox.Show(latestBuildPhase);


                    // Check if Glow is the Latest Version
                    // Update Available
                    if (latestVersion > currentVersion)
                    {
                        // Yes/No Dialog Confirmation
                        //
                        MessageBoxResult result = MessageBox.Show("v" + latestVersion + "-" + latestBuildPhase + "\n\nDownload Update?", "Update Available ", MessageBoxButton.YesNo);
                        switch (result)
                        {
                            case MessageBoxResult.Yes:
                                // Detect which screen we're on
                                var allScreens = System.Windows.Forms.Screen.AllScreens.ToList();
                                var thisScreen = allScreens.SingleOrDefault(s => this.Left >= s.WorkingArea.Left && this.Left < s.WorkingArea.Right);

                                // Start Window
                                UpdateWindow updatewindow = new UpdateWindow();

                                // Keep in Front
                                updatewindow.Owner = Window.GetWindow(this);

                                // Only allow 1 Window instance
                                if (IsUpdateWindowOpened) return;
                                updatewindow.ContentRendered += delegate { IsUpdateWindowOpened = true; };
                                updatewindow.Closed += delegate { IsUpdateWindowOpened = false; };

                                // Position Relative to MainWindow
                                // Keep from going off screen
                                updatewindow.Left = Math.Max((this.Left + (this.Width - updatewindow.Width) / 2), thisScreen.WorkingArea.Left);
                                updatewindow.Top = Math.Max((this.Top + (this.Height - updatewindow.Height) / 2), thisScreen.WorkingArea.Top);

                                // Open Window
                                updatewindow.Show();
                                break;
                            case MessageBoxResult.No:
                                break;
                        }
                    }
                    // Update Not Available
                    else if (latestVersion <= currentVersion)
                    {
                        MessageBox.Show("This version is up to date.",
                                       "Notice",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Information);

                        return;
                    }
                    // Unknown
                    else // null
                    {
                        MessageBox.Show("Could not find download. Try updating manually.",
                                        "Error",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);

                        return;
                    }
                }
                // Version is Null
                else
                {
                    MessageBox.Show("GitHub version file returned empty.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);

                    return;

                }
            }
            else
            {
                MessageBox.Show("Could not detect Internet Connection.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);

                return;
            }
        }

        /// <summary>
        ///    Open Config Directory
        /// </summary>
        private void buttonConfigDir_Click(object sender, RoutedEventArgs e)
        {
            // Check if Config Directory exists
            //bool exists = Directory.Exists(vm.mpvConfigPath_Text);
            // If not, create it
            if (!Directory.Exists(vm.mpvConfigPath_Text))
            {
                // Yes/No Dialog Confirmation
                //
                MessageBoxResult resultOpen = MessageBox.Show("Config Folder does not exist. Automatically reate it?", 
                                                              "Directory Not Found", 
                                                              MessageBoxButton.YesNo, 
                                                              MessageBoxImage.Information);
                switch (resultOpen)
                {
                    // Create
                    case MessageBoxResult.Yes:
                        try
                        {
                            Directory.CreateDirectory(vm.mpvConfigPath_Text);
                        }
                        catch
                        {
                            MessageBox.Show("Could not create Config folder. May require Administrator privileges.", 
                                            "Error", 
                                            MessageBoxButton.OK, 
                                            MessageBoxImage.Error);
                        }
                        break;
                    // Use Default
                    case MessageBoxResult.No:
                        break;
                }
            }


            // Check if mpv config dir exists
            // If not, create it
            //Directory.CreateDirectory(configDir);

            // Open Directory
            if (Directory.Exists(vm.mpvConfigPath_Text))
            {
                Process.Start("explorer.exe", vm.mpvConfigPath_Text);
            }
                
        }


        /// <summary>
        ///     Copy Config
        /// </summary>
        private void buttonCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ConfigRichTextBox(), TextDataFormat.UnicodeText);
        }


        /// <summary>
        ///     Save Config Button
        /// </summary>
        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            // Check if Config Directory exists
            bool exists = Directory.Exists(vm.mpvConfigPath_Text);
            // If not, create it
            if (!exists)
            {
                // Yes/No Dialog Confirmation
                //
                MessageBoxResult resultSave = MessageBox.Show(
                    "Config Folder does not exist. Automatically create it?", 
                    "Directory Not Found", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Information);
                switch (resultSave)
                {
                    // Create
                    case MessageBoxResult.Yes:
                        try
                        {
                            Directory.CreateDirectory(vm.mpvConfigPath_Text);
                        }
                        catch
                        {
                            MessageBox.Show(
                                "Could not create Config folder. May require Administrator privileges.", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
                        }
                        break;
                    // Use Default
                    case MessageBoxResult.No:
                        break;
                }
            }

            // Open 'Save File'
            Microsoft.Win32.SaveFileDialog saveFile = new Microsoft.Win32.SaveFileDialog();

            saveFile.InitialDirectory = vm.mpvConfigPath_Text;
            saveFile.RestoreDirectory = true;
            saveFile.Filter = "Config Files (*.conf)|*.conf";
            saveFile.DefaultExt = "";
            saveFile.FileName = "mpv.conf";

            // Show save file dialog box
            Nullable<bool> result = saveFile.ShowDialog();

            // Process dialog box
            if (result == true)
            {
                // Check for Save Error
                try
                {
                    // Save document
                    File.WriteAllText(saveFile.FileName, ConfigRichTextBox(), Encoding.UTF8);
                }
                catch
                {
                    MessageBox.Show("Problem Saving Config to " + "\"" + vm.mpvConfigPath_Text + "\"" + ". May require Administrator Privileges.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }


        /// <summary>
        ///     Subtitle Color Picker
        /// </summary>
        private Boolean IsColorPickerWindowOpened = false;

        /// <summary>
        ///     Open Color Picker Window
        /// </summary>
        // Passing the TextBox Keyword will identify which TextBox to return the number to
        public void OpenColorPickerWindow(string textBoxKey)
        {
            // Detect which screen we're on
            var allScreens = System.Windows.Forms.Screen.AllScreens.ToList();
            var thisScreen = allScreens.SingleOrDefault(s => this.Left >= s.WorkingArea.Left && this.Left < s.WorkingArea.Right);

            // Start Window
            ColorPickerWindow colorPickerWindow = new ColorPickerWindow(this, textBoxKey);

            // Keep Window on Top
            colorPickerWindow.Owner = GetWindow(this);

            // Only allow 1 Window instance
            if (IsColorPickerWindowOpened) return;
            colorPickerWindow.ContentRendered += delegate { IsColorPickerWindowOpened = true; };
            colorPickerWindow.Closed += delegate { IsColorPickerWindowOpened = false; };

            // Position Relative to MainWindow
            colorPickerWindow.Left = Math.Max((this.Left + (this.Width - colorPickerWindow.Width) / 2), thisScreen.WorkingArea.Left);
            colorPickerWindow.Top = Math.Max((this.Top + (this.Height - colorPickerWindow.Height) / 2), thisScreen.WorkingArea.Top);

            // Open Window
            colorPickerWindow.ShowDialog();
        }




        /// <summary>
        ///    Preset
        /// </summary>
        private void cboProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Profiles.Profile(this, vm);

            //MessageBox.Show(vm.Profiles_SelectedItem); //deubg
        }

        /// <summary>
        ///    Export Preset
        /// </summary>
        private void buttonExport_Click(object sender, RoutedEventArgs e)
        {
            // Check if Profiles Directory exists
            //bool exists = Directory.Exists(vm.ProfilesPath_Text);
            // If not, create it
            if (!Directory.Exists(vm.ProfilesPath_Text))
            {
                // Yes/No Dialog Confirmation
                //
                MessageBoxResult resultExport = MessageBox.Show("Profiles Folder does not exist. Automatically create it?", 
                                                                "Directory Not Found", 
                                                                MessageBoxButton.YesNo, 
                                                                MessageBoxImage.Information);
                switch (resultExport)
                {
                    // Create
                    case MessageBoxResult.Yes:
                        try
                        {
                            Directory.CreateDirectory(vm.ProfilesPath_Text);
                        }
                        catch
                        {
                            MessageBox.Show("Could not create Profiles folder. May require Administrator privileges.", 
                                            "Error", 
                                            MessageBoxButton.OK, 
                                            MessageBoxImage.Error);
                        }
                        break;
                    // Use Default
                    case MessageBoxResult.No:
                        break;
                }
            }

            // Open 'Save File'
            Microsoft.Win32.SaveFileDialog saveFile = new Microsoft.Win32.SaveFileDialog();

            // Defaults
            saveFile.InitialDirectory = vm.ProfilesPath_Text;
            saveFile.RestoreDirectory = true;
            saveFile.Filter = "Initialization Files (*.ini)|*.ini";
            saveFile.DefaultExt = "";
            saveFile.FileName = "profile.ini";

            // Show save file dialog box
            Nullable<bool> result = saveFile.ShowDialog();

            // Process dialog box
            if (result == true)
            {
                // Set Input Dir, Name, Ext
                string inputDir = Path.GetDirectoryName(saveFile.FileName).TrimEnd('\\') + @"\";
                //string inputFileName = Path.GetFileName(saveFile.FileName);
                string inputFileName = Path.GetFileNameWithoutExtension(saveFile.FileName);
                string inputExt = Path.GetExtension(saveFile.FileName);
                string input = inputDir + inputFileName + inputExt;
                //string input = Path.Combine(inputDir, inputFileName);

                // Overwriting doesn't work properly with INI Writer
                // Delete File instead before saving new
                if (File.Exists(input))
                {
                    File.Delete(input);
                }

                // Export ini file
                Profiles.ExportProfile(this, vm, input);

                // Refresh Profiles ComboBox
                Profiles.GetCustomProfiles(vm);

                //ViewModel vm = this.DataContext as ViewModel;
                cboProfile.ItemsSource = vm.Profiles_Items;
                //cboProfile.ItemsSource = ViewModel._profilesItems;
            }
        }


        /// <summary>
        ///    Import Preset
        /// </summary>
        private void buttonImport_Click(object sender, RoutedEventArgs e)
        {
            // Check if presets directory exists
            // If not, create it
            //Directory.CreateDirectory(profilesDir);

            // Open 'Select File'
            Microsoft.Win32.OpenFileDialog selectFile = new Microsoft.Win32.OpenFileDialog();

            // Defaults
            selectFile.InitialDirectory = vm.ProfilesPath_Text;
            selectFile.RestoreDirectory = true;
            selectFile.Filter = "ini file (*.ini)|*.ini";

            // Show select file dialog box
            Nullable<bool> result = selectFile.ShowDialog();

            // Process dialog box
            if (result == true)
            {
                // Set Input Dir, Name, Ext
                string inputDir = Path.GetDirectoryName(selectFile.FileName).TrimEnd('\\') + @"\";
                //string inputFileName = Path.GetFileName(selectFile.FileName);
                string inputFileName = Path.GetFileNameWithoutExtension(selectFile.FileName);
                string inputExt = Path.GetExtension(selectFile.FileName);
                string input = inputDir + inputFileName + inputExt;
                //string input = Path.Combine(inputDir, inputFileName);

                // Import ini file
                Profiles.ImportProfile(this, vm, input);
            }
        }


        /// <summary>
        ///     Generate
        /// </summary>
        private void buttonGenerate_Click(object sender, RoutedEventArgs e)
        {
            // Write Config to RichTextBox
            Paragraph p = new Paragraph();
            p.LineHeight = 2;
            rtbConfig.Document = new FlowDocument(p);

            rtbConfig.BeginChange();
            p.Inlines.Add(new Run(Generate.GenerateConfig(this, vm)));
            rtbConfig.EndChange();
        }


    }
}
