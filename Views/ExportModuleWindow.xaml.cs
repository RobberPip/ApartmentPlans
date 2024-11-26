using ApartmentPlans.Domain;
using ApartmentPlans.ViewModels;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using System.Reflection;
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
        private void ComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var comboBox = sender as ComboBox;
            comboBox.SelectedIndex = -1;
        }
        private MainVm mainVM;
        public ExportModuleWindow(UIApplication application)
        {
            var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            var directory = System.IO.Path.GetDirectoryName(executingAssembly.Location);
            var dllPath = System.IO.Path.Combine(directory, "ColorPicker.dll");
            var dllPath1 = System.IO.Path.Combine(directory, "Microsoft.Xaml.Behaviors.dll");
            Assembly.LoadFrom(dllPath);
            Assembly.LoadFrom(dllPath1);
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
            if (!mainVM.ItemsSchedule.Any(q => q.ObjectParameters.Any(w => w.Name == text)))
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
        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            for (int i = 0; i < e.AddedItems.Count; i++)
            {

                var view = e.AddedItems[i] as WorkViewModel;
                if (view != null)
                {
                    // Переключаем IsSelected
                    view.IsSelected = !view.IsSelected;
                }
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var view in mainVM.ViewsSheet)
            {
                view.IsSelected = true;
            }
        }
        private void CheckBox_UnChecked(object sender, RoutedEventArgs e)
        {
            foreach (var view in mainVM.ViewsSheet)
            {
                view.IsSelected = false;
            }
        }
        private void TextBoxOffset_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == "-")
            {
                // Блокируем минус, если он не первый символ или уже есть в тексте
                e.Handled = ((sender as TextBox).Text.Contains("-") || (sender as TextBox).CaretIndex != 0);
            }
            else
            {
                // Блокируем всё, кроме цифр
                e.Handled = !int.TryParse(e.Text, out _);
            }
        }
        private void TextBoxOffset_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Блокируем нажатие клавиши пробела
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
    }
}
