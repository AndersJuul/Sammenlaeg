using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using Google.OrTools.LinearSolver;
using Newtonsoft.Json;
using Serilog;

namespace Sammenlaeg.Wpf
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public readonly BackgroundWorker Bwt;
        private ObservableCollection<string> _logLines = new ObservableCollection<string>();

        public MainWindowViewModel()
        {
            Bwt = new BackgroundWorker();
            Bwt.DoWork += DoWork;
            Bwt.RunWorkerCompleted += RunWorkerCompleted;
            Bwt.RunWorkerAsync();
        }

        [JsonIgnore] public ICommand UploadCommand { get; set; }

        [JsonIgnore]
        public ObservableCollection<string> LogLines
        {
            get => _logLines;
            set
            {
                _logLines = value;
                NotifyPropertyChanged();
            }
        }

        public Dispatcher Dispatcher { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                CommandManager.InvalidateRequerySuggested();

                Log.Logger.Information("----------------------------------------------"); Thread.Sleep(50);
                Log.Logger.Information("Starter "); Thread.Sleep(50);
                NotifyPropertyChanged(nameof(LogLines));

                var pupils = GetPupils().ToArray();
                Log.Logger.Information($"Læste {pupils.Length} elever"); Thread.Sleep(50);
                var classes = GetClasses().ToArray();
                Log.Logger.Information($"Læste {classes.Length} klasser"); Thread.Sleep(50);
                var oensker = GetOensker().ToArray();
                Log.Logger.Information($"Læste {oensker.Length} ønsker"); Thread.Sleep(50);

                // ---------
                // Create the linear solver with the GLOP backend.
                var solver = Solver.CreateSolver("GLOP");

                // Create the variables for pupil-presence-in-class.
                // All integer and 0/1 (meaning in or not in class)
                foreach (var elevDto in pupils)
                {
                    foreach (var @class in classes)
                    {
                        var varname = GetPupilInClassVarname(elevDto, @class);
                        solver.MakeNumVar(0.0, 1.0, varname).SetInteger(true);
                        solver.MakeConstraint(0.0, 1.0, varname);
                    }
                }

                // Create the variables for oensker-match-in-class.
                foreach (var oenskeDto in oensker)
                {
                    foreach (var @class in classes)
                    {
                        var varname = GetOenskeMatchInClassVarname(oenskeDto, @class);
                        solver.MakeNumVar(0.0, 2.0, varname).SetInteger(true);
                        solver.MakeConstraint(0.0, 2.0, varname);
                    }
                }

                Log.Logger.Information("Number of variables = " + solver.NumVariables()); Thread.Sleep(50);

                // Create constraint, pupil in one class: 0 <= Sum(presence in each class) <= 1.
                foreach (var elevDto in pupils)
                {
                    var constraintOnlyInOneClass = solver.MakeConstraint(0.0, 1.0, $"PupilInOneClass.{elevDto.Id}.{elevDto.Name}");
                    foreach (var @class in classes)
                    {
                        var variable = solver.variables().Single(x => x.Name() == GetPupilInClassVarname(elevDto, @class));
                        constraintOnlyInOneClass.SetCoefficient(variable, 1);
                    }
                }
                // Create constraint, max in one class: 0 <= Sum(presence in each class) <= 1.
                foreach (var @class in classes)
                {
                    var constraintMaxPerClass = solver.MakeConstraint(0.0, @class.MaxInClass, $"MaxPerClass.{@class.Name}");
                    foreach (var elevDto in pupils)
                    {
                        var variable = solver.variables().Single(x => x.Name() == GetPupilInClassVarname(elevDto, @class));
                        constraintMaxPerClass.SetCoefficient(variable, 1);
                    }
                }

                Log.Logger.Information("Number of constraints = " + solver.NumConstraints()); Thread.Sleep(50);

                var objective = solver.Objective();
                foreach (var elevDto in pupils)
                {
                    foreach (var @class in classes)
                    {
                        var variable = solver.variables().Single(x => x.Name() == GetPupilInClassVarname(elevDto, @class));
                        objective.SetCoefficient(variable, 1);
                    }
                }
                objective.SetMaximization();

                solver.Solve();

                Log.Logger.Information("Solution:Objective value = " + solver.Objective().Value()); Thread.Sleep(50);
                foreach (var variable in solver.variables().Where(x=>x.SolutionValue()>=0))
                {
                    Log.Logger.Information(variable.Name() + " = " + variable.SolutionValue()); Thread.Sleep(50);
                }

                foreach (var klasseDto in classes)
                {
                    var inClass = new List<ElevDto>();
                    foreach (var elevDto in pupils)
                    {
                        var variable = solver.variables().Single(x => x.Name() == GetPupilInClassVarname(elevDto, klasseDto));
                        if (variable.SolutionValue() > 0)
                            inClass.Add(elevDto);
                    }
                    Log.Logger.Information($"I klasse {klasseDto.Name},{inClass.Count}: {string.Join(";", inClass.Select(x => x.Name))}"); Thread.Sleep(50);
                }
                Log.Logger.Information("----------------------------------------------"); Thread.Sleep(50);
                // ---------
            }
            catch (Exception exception)
            {
                Log.Logger.Error(exception, "Under optimering");
            }
        }

        private string GetOenskeMatchInClassVarname(OenskeDto oenskeDto, KlasseDto @class)
        {
            return $"OenskeMatchInClass.{oenskeDto.ElevId1}.{oenskeDto.ElevId2}.{@class.Name}";
        }

        private static string GetPupilInClassVarname(ElevDto elevDto, KlasseDto @class)
        {
            return $"IsInClass.{elevDto.Id}.{elevDto.Name}.{@class.Name}";
        }

        private static ElevDto[] GetPupils()
        {
            using var reader = new StreamReader(@".\elever.csv");
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { });
            return csv.GetRecords<ElevDto>().ToArray();
        }
        private static KlasseDto[] GetClasses()
        {
            using var reader = new StreamReader(@".\klasser.csv");
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { });
            return csv.GetRecords<KlasseDto>().ToArray();
        }

        private static OenskeDto[] GetOensker()
        {
            using var reader = new StreamReader(@".\oensker.csv");
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { });
            return csv.GetRecords<OenskeDto>().ToArray();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}