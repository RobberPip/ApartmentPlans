using ApartmentPlans.Domain;
using ApartmentPlans.ViewModels;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime;
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
    public partial class MainWindow : Window
    {
        private MainVm mainVM;

        public MainWindow(UIApplication application)
        {
            InitializeComponent();
            mainVM = new MainVm(application);
            DataContext = mainVM;
            mainVM.LabelDoorCountText = mainVM.LabelWallCountText =mainVM.LabelWallFacadesCountText ="Выбрано: 0";


        }
        private void ComboBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                comboBox.IsDropDownOpen = true;
            }
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
        private void SortParameterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.SelectedItem != null && comboBox.SelectedItem is SelectableParameterModel selectedParameter)
            {
                mainVM.UpdateValidValuesSortParameters(selectedParameter.parameter.Definition.Name);
            }
        }
        private void CheckBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Предотвращаем закрытие ComboBox
            e.Handled = true;

            if (sender is CheckBox checkBox)
            {
                // Изменяем состояние IsChecked на противоположное
                checkBox.IsChecked = !checkBox.IsChecked;
            }
        }


        private void ValueSortParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.ValueSortParameters.Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void СolumnParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.СolumnParameters.Select(q => q.Definition.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void NumApartParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.NumApartParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void TypesSquareComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.TypesSquare.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void TypeApartmentParameterComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.TypeApartmentParameters.Select(q => q.Name).Contains(text))
            {
                comboBox.Text = string.Empty;
            }
        }
        private void TypesComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            comboBox.SelectedItem = null;
        }
        private void LabelDoorCount_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (doorTypeComboBox != null)
            {
                doorTypeComboBox.IsDropDownOpen = true;
                doorTypeComboBox.Focus();
                e.Handled = true;
            }
        }
        private void LabelWallCount_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (wallTypeComboBox != null)
            {
                wallTypeComboBox.IsDropDownOpen = true;
                wallTypeComboBox.Focus();
                e.Handled = true;
            }
        }
        private void LabelWallFacadesCount_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (wallFacadesTypeComboBox != null)
            {
                wallFacadesTypeComboBox.IsDropDownOpen = true;
                wallFacadesTypeComboBox.Focus();
                e.Handled = true;
            }
        }


        private void CheckBoxDoor_Checked(object sender, EventArgs e)
        {
            mainVM.LabelDoorCountText = "Выбарно: " + mainVM.DoorTypes.Count(q => q.IsSelected == true).ToString();
        }
        private void CheckBoxWall_Checked(object sender, EventArgs e)
        {
            mainVM.LabelWallCountText = "Выбарно: " + mainVM.WallTypes.Count(q => q.IsSelected == true).ToString();
        }
        private void CheckBoxWallFacades_Checked(object sender, EventArgs e)
        {
            mainVM.LabelWallFacadesCountText = "Выбарно: " + mainVM.WallFacadesTypes.Count(q => q.IsSelected == true).ToString();
        }

        private void TextBoxSearchViews_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox.Text.Length > 0 && textBox.Text != FileResources.Search)
            {
                mainVM.Views.Clear();
                foreach (var view in mainVM._tempViews.Where(q => q.View.Name.ToLower().Contains(textBox.Text.ToLower())))
                {
                    mainVM.Views.Add(view);
                }
            }
            else if (textBox.Text != FileResources.Search)
            {
                mainVM.Views.Clear();
                foreach (var view in mainVM._tempViews)
                {
                    mainVM.Views.Add(view);
                }
            }
        }
        private void CheckBoxExport_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            Brush foregroundColor = checkBox.IsChecked == true ? Brushes.Black : Brushes.LightGray;

            TextBlock textBlock;

            textBlock = FindName("NameProject") as TextBlock;
            textBlock.Foreground = foregroundColor;

            textBlock = FindName("NameCase") as TextBlock;
            textBlock.Foreground = foregroundColor;

            textBlock = FindName("NameSection") as TextBlock;
            textBlock.Foreground = foregroundColor;

            textBlock = FindName("NameFloor") as TextBlock;
            textBlock.Foreground = foregroundColor;

            textBlock = FindName("PathToSave") as TextBlock;
            textBlock.Foreground = foregroundColor;

            textBlock = FindName("Path") as TextBlock;
            textBlock.Foreground = foregroundColor;

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
        private void TypesLegendComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var text = comboBox.Text;
            if (!mainVM.TypesLegend.Select(q => q.Name).Contains(text))
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
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
            if (textBox != null)
            {
                if (textBox.Text.Length == 0 && e.Text == "0")
                {
                    e.Handled = true;
                }
                else if (textBox.Text == "0" && textBox.SelectionStart == 1)
                {
                    e.Handled = true;
                }
            }
        }
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
