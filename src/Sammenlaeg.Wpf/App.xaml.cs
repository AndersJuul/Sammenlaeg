using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace Sammenlaeg.Wpf
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly BackgroundWorker _bwt;

        public App()
        {
            var sammenlaegFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Sammenlaeg");
            Directory.CreateDirectory(sammenlaegFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(sammenlaegFolder, "logfile.log"), rollingInterval: RollingInterval.Day)
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200")))
                .CreateLogger();

            var settingsFile = sammenlaegFolder + "\\settings.json";
            MainWindowViewModel mainWindowViewModel;
            try
            {
                Log.Logger.Information("Forsøger at indlæse indstillinger fra " + settingsFile);
                mainWindowViewModel =
                    JsonConvert.DeserializeObject<MainWindowViewModel>(File.ReadAllText(settingsFile));
            }
            catch (Exception e)
            {
                Log.Logger.Error(e, "Fejl ifbm indlæsning af indstillinger fra " + settingsFile);

                mainWindowViewModel = new MainWindowViewModel();
            }

            mainWindowViewModel.UploadCommand = new RelayCommand(
                vm => { vm.Bwt.RunWorkerAsync(); },
                vm => !vm.Bwt.IsBusy,
                mainWindowViewModel);

            mainWindowViewModel.Dispatcher = Dispatcher;

            var view = new MainWindow {DataContext = mainWindowViewModel};
            view.ShowDialog();

            File.WriteAllText(settingsFile, JsonConvert.SerializeObject(mainWindowViewModel, Formatting.Indented));
            Log.Information("Afsluttede program.");

            Log.CloseAndFlush();
        }
    }
}