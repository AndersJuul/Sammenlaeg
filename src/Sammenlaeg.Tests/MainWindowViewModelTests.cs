using System;
using System.ComponentModel;
using System.IO;
using NUnit.Framework;
using Sammenlaeg.Wpf;
using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace Sammenlaeg.Tests
{
    [TestFixture]
    public class MainWindowViewModelTests
    {
        [Test]
        public void T()
        {
            // Arrange
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200")))
                .CreateLogger();

            var sut = new MainWindowViewModel();

            // Act
            //sut.DoWork(null, new DoWorkEventArgs(null));

            // Assert
        }
    }
}
