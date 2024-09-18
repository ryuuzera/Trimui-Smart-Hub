﻿using System;
using System.Collections.Generic;
using System.Linq;
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

namespace TrimuiSmartHub.Application.Components
{
    public partial class CustomDialog : UserControl
    {
        public CustomDialog()
        {
            InitializeComponent();
        }

        // Método para esconder o modal
        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
