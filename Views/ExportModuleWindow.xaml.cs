using ApartmentPlans.ViewModels;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ComboBox = System.Windows.Controls.ComboBox;
using FileResources = ApartmentPlans.Resources.FileResources;
using TextBox = System.Windows.Controls.TextBox;

namespace ApartmentPlans
{
    /// <summary>
    /// Логика взаимодействия для ExportModuleWindow.xaml
    /// </summary>
    public partial class ExportModuleWindow : Window
    {
        private MainVm mainVM;
        public ExportModuleWindow(UIApplication application)
        {
            InitializeComponent();
            mainVM = new MainVm(application);
            DataContext = mainVM;
        }
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && textBox.Text == FileResources.Search)
            {
                textBox.Text = string.Empty;
            }
        }
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = FileResources.Search;
            }
        }
        private void SortParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.SortParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void TextBoxSearchViews_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox.Text.Length > 0 && textBox.Text != FileResources.Search)
            {
                mainVM.ViewsSheet.Clear();
                foreach (var view in mainVM._tempViewsSheet.Where(q => q.View.Name.ToLower().Contains(textBox.Text.ToLower())))
                {
                    mainVM.ViewsSheet.Add(view);
                }
            }
            else if (textBox.Text != FileResources.Search)
            {
                mainVM.ViewsSheet.Clear();
                foreach (var view in mainVM._tempViewsSheet)
                {
                    mainVM.ViewsSheet.Add(view);
                }
            }
        }
        private void NamesCaseParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.NamesCaseParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void NamesSectionParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.NamesSectionParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void NamesFloorParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.NamesFloorParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBox.IsDropDownOpen = true;
            }
        }

    }
}
