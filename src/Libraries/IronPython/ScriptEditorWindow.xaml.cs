﻿using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using Dynamo.Controls;
using Dynamo.Python;
using Dynamo.ViewModels;

using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace DSIronPythonNode
{
    /// <summary>
    /// Interaction logic for ScriptEditorWindow.xaml
    /// </summary>
    public partial class ScriptEditorWindow : Window
    {
        private string propertyName = string.Empty;
        private System.Guid boundNodeId = System.Guid.Empty;
        private CompletionWindow completionWindow = null;
        private readonly IronPythonCompletionProvider completionProvider;
        private readonly DynamoViewModel dynamoViewModel;

        public ScriptEditorWindow(DynamoViewModel dynamoViewModel)
        {
            this.dynamoViewModel = dynamoViewModel;
            this.completionProvider = new IronPythonCompletionProvider(dynamoViewModel.Model.Logger);

            InitializeComponent();
            var view = FindUpVisualTree<DynamoView>(this);
            this.Owner = view;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        internal void Initialize(System.Guid nodeGuid, string propName, string propValue)
        {
            this.boundNodeId = nodeGuid;
            this.propertyName = propName;

            // Register auto-completion callbacks
            this.editText.TextArea.TextEntering += OnTextAreaTextEntering;
            this.editText.TextArea.TextEntered += OnTextAreaTextEntered;

            const string highlighting = "ICSharpCode.PythonBinding.Resources.Python.xshd";
            var elem = GetType().Assembly.GetManifestResourceStream(
                        "DSIronPythonNode.Resources." + highlighting);

            this.editText.SyntaxHighlighting = HighlightingLoader.Load(
                new XmlTextReader(elem), HighlightingManager.Instance);

            this.editText.Text = propValue;
            this.Closed += OnScriptEditWindowClosed;
        }

        #region Autocomplete Event Handlers

        private void OnTextAreaTextEntering(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (e.Text.Length > 0 && completionWindow != null)
                {
                    if (!char.IsLetterOrDigit(e.Text[0]))
                        completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            catch (System.Exception ex)
            {
                this.dynamoViewModel.Model.Logger.Log("Failed to perform python autocomplete with exception:");
                this.dynamoViewModel.Model.Logger.Log(ex.Message);
                this.dynamoViewModel.Model.Logger.Log(ex.StackTrace);
            }
        }

        private void OnTextAreaTextEntered(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (e.Text == ".")
                {
                    var subString = editText.Text.Substring(0, this.editText.CaretOffset);
                    var completions = completionProvider.GetCompletionData(subString);

                    if (completions.Length == 0)
                        return;

                    completionWindow = new CompletionWindow(this.editText.TextArea);
                    var data = completionWindow.CompletionList.CompletionData;

                    foreach (var completion in completions)
                        data.Add(completion);

                    completionWindow.Show();
                    completionWindow.Closed += delegate
                    {
                        completionWindow = null;
                    };
                }
            }
            catch (System.Exception ex)
            {
                this.dynamoViewModel.Model.Logger.Log("Failed to perform python autocomplete with exception:");
                this.dynamoViewModel.Model.Logger.Log(ex.Message);
                this.dynamoViewModel.Model.Logger.Log(ex.StackTrace);
            }
        }

        #endregion

        #region Private Event Handlers

        private void OnAcceptClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OnScriptEditWindowClosed(object sender, System.EventArgs e)
        {
            if (this.DialogResult.HasValue && (this.DialogResult.Value))
            {
                var command = new DynamoViewModel.UpdateModelValueCommand(
                    this.boundNodeId, this.propertyName, this.editText.Text);

                this.dynamoViewModel.ExecuteCommand(command);
            }
        }

        #endregion

        // walk up the visual tree to find object of type T, starting from initial object
        private static T FindUpVisualTree<T>(DependencyObject initial) where T : DependencyObject
        {
            DependencyObject current = initial;

            while (current != null && current.GetType() != typeof(T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }
    }
}
