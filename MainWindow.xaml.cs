using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Win32;
using System.Windows.Forms;

namespace DesktopWidget
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly MMDeviceEnumerator _deviceEnum = new MMDeviceEnumerator();
        private MMDevice _selectedOutputDevice = null;
        private MMDevice _selectedInputDevice = null;
        private NotifyIcon _notifyIcon;
        private AppSettings _settings;
        private const string SETTINGS_FILE = "widget_settings.json";

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitTray();
            InitClock();
            LoadAudioDevices();
            LoadSavedCity();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) { }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void InitClock()
        {
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _clockTimer.Start();
        }

        private void LoadAudioDevices()
        {
            var renderers = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            OutputDevicesCombo.ItemsSource = renderers.Select(d => new DeviceItem(d)).ToList();
            var captures = _deviceEnum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            InputDevicesCombo.ItemsSource = captures.Select(d => new DeviceItem(d)).ToList();
            var defaultRender = _deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var item = OutputDevicesCombo.Items.Cast<DeviceItem>().FirstOrDefault(x => x.Id == defaultRender.ID);
            if (item != null) { OutputDevicesCombo.SelectedItem = item; _selectedOutputDevice = defaultRender; UpdateVolumeSliderFromDevice(); }
            var defaultCapture = _deviceEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            var item2 = InputDevicesCombo.Items.Cast<DeviceItem>().FirstOrDefault(x => x.Id == defaultCapture.ID);
            if (item2 != null) { InputDevicesCombo.SelectedItem = item2; _selectedInputDevice = defaultCapture; }
        }

        private void OutputDevicesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (OutputDevicesCombo.SelectedItem is DeviceItem di)
            {
                _selectedOutputDevice = _deviceEnum.GetDevice(di.Id);
                UpdateVolumeSliderFromDevice();
            }
        }

        private void InputDevicesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (InputDevicesCombo.SelectedItem is DeviceItem di)
            {
                _selectedInputDevice = _deviceEnum.GetDevice(di.Id);
            }
        }

        private void UpdateVolumeSliderFromDevice()
        {
            if (_selectedOutputDevice != null)
            {
                var vol = _selectedOutputDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0f;
                VolumeSlider.Value = vol;
                VolumeLabel.Text = $"{(int)vol}%";
            }
        }

        private void VolumeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            var v = (float)(VolumeSlider.Value / 100.0);
            VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
            if (_selectedOutputDevice != null)
            {
                try { _selectedOutputDevice.AudioEndpointVolume.MasterVolumeLevelScalar = v; }
                catch { }
            }
            else
            {
                var def = _deviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                try { def.AudioEndpointVolume.MasterVolumeLevelScalar = v; } catch { }
            }
        }

        private async void LoadSavedCity()
        {
            if (!string.IsNullOrEmpty(_settings?.WeatherCity))
            {
                WeatherCityBox.Text = _settings.WeatherCity;
                await RefreshWeatherAsync(_settings.WeatherCity);
            }
        }

        private async void BtnRefreshWeather_Click(object sender, RoutedEventArgs e)
        {
            var city = WeatherCityBox.Text.Trim();
            if (!string.IsNullOrEmpty(city)) { await RefreshWeatherAsync(city); }
        }

        private void BtnSaveCity_Click(object sender, RoutedEventArgs e)
        {
            var city = WeatherCityBox.Text.Trim();
            if (string.IsNullOrEmpty(city)) return;
            _settings.WeatherCity = city;
            SaveSettings();
            WeatherNote.Text = "지역 저장됨";
        }

        private async Task RefreshWeatherAsync(string city)
        {
            WeatherNote.Text = "로딩 중...";
            try
            {
                if (string.IsNullOrEmpty(_settings.OpenWeatherApiKey))
                {
                    WeatherNote.Text = "OpenWeather API 키를 설정하세요 (settings 파일)."; return;
                }
                using var cli = new HttpClient();
                var q = System.Web.HttpUtility.UrlEncode($"{city},KR");
                var url = $"https://api.openweathermap.org/data/2.5/weather?q={q}&appid={_settings.OpenWeatherApiKey}&units=metric&lang=kr";
                var res = await cli.GetStringAsync(url);
                var j = JObject.Parse(res);
                var name = j["name"]?.ToString();
                var main = j["weather"]?.First?[""main""]?.ToString();
                var desc = j["weather"]?.First?[""description""]?.ToString();
                var temp = j["main"]?[""temp""]?.ToObject<double?>();

                WeatherLocation.Text = name ?? city;
                WeatherMain.Text = main ?? "";
                WeatherDesc.Text = desc ?? "";
                WeatherTemp.Text = temp.HasValue ? $"{temp.Value:F1} °C" : "-";
                WeatherNote.Text = $"갱신 시각: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex) { WeatherNote.Text = "날씨 불러오는 중 오류: " + ex.Message; }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SETTINGS_FILE))
                {
                    var t = File.ReadAllText(SETTINGS_FILE);
                    _settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(t);
                }
            }
            catch { }
            if (_settings == null) _settings = new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var t = Newtonsoft.Json.JsonConvert.SerializeObject(_settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, t);
            }
            catch { }
        }

        private void InitTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; this.Activate(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("열기", null, (s, e) => { this.Show(); });
            menu.Items.Add("자동시작 토글", null, (s, e) => ToggleAutostart());
            menu.Items.Add("종료", null, (s, e) => { _notifyIcon.Visible = false; System.Windows.Application.Current.Shutdown(); });
            _notifyIcon.ContextMenuStrip = menu;
        }

        private void BtnMenu_Click(object sender, RoutedEventArgs e) { _notifyIcon.ContextMenuStrip.Show(System.Windows.Forms.Control.MousePosition); }

        private void ToggleAutostart()
        {
            string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string appName = "MyWidgetApp";
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true);
            if (key.GetValue(appName) == null) { key.SetValue(appName, $"\"{exePath}\""); System.Windows.MessageBox.Show("자동시작 등록됨"); }
            else { key.DeleteValue(appName); System.Windows.MessageBox.Show("자동시작 해제됨"); }
        }
    }

    class DeviceItem
    {
        public string Id { get; }
        public string FriendlyName { get; }
        public DeviceItem(NAudio.CoreAudioApi.MMDevice d) { Id = d.ID; FriendlyName = d.FriendlyName; }
        public override string ToString() => FriendlyName;
    }

    class AppSettings
    {
        public string WeatherCity { get; set; } = "";
        public string OpenWeatherApiKey { get; set; } = "";
    }
}
