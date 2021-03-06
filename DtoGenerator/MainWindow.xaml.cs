﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DtoGenerator.Annotations;
using DtoGenerator.Properties;
using MahApps.Metro.Controls;


namespace DtoGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        public string ConnectionString { get; set; }
        public string SqlQuery { get; set; }
        private string _output;

        public string Output
        {
            get => _output;
            set
            {
                if (value == _output) return;
                _output = value;
                OnPropertyChanged();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            ConnectionString = Settings.Default.ConnectionString;
            SqlQuery = Settings.Default.SqlQuery;

        }

        private void BtGenerate_Click(object sender, RoutedEventArgs e)
        {
            SaveRecents();
            GenerateCode(ConnectionString, SqlQuery);
        }

        private void SaveRecents()
        {
            Settings.Default["ConnectionString"] = ConnectionString;
            Settings.Default["SqlQuery"] = SqlQuery;
            Settings.Default.Save();
        }

        private void GenerateCode(string connectionString, string sql)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new Exception("Database Connection String is Required");
            if (string.IsNullOrEmpty(sql)) throw new Exception("Query String is Required");

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    Output = ADOHelper.DumpClass(connection, sql);
                }
            }
            catch (Exception)
            {

                // do nothing
            }
            
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
