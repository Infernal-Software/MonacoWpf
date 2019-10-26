using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Monaco.Wpf
{
    public class MonacoEditor : UserControl
    {
        public EventHandler OnEditorInitialized;
        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set
            {
                if (Value != value)
                {
                    SetValue(ValueProperty, value);
                }
        }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(MonacoEditor),
                new FrameworkPropertyMetadata
                {
                    DefaultValue = "",
                    BindsTwoWayByDefault = true,
                    DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    PropertyChangedCallback = (o, m) => (o as MonacoEditor)?.UpdateValue()
                });
            //    new PropertyMetadata()
            //{
            //    DefaultValue = "",

                //    PropertyChangedCallback = (o, m) => (o as MonacoEditor)?.UpdateValue()
                //});
        public void UpdateValue()
        {
            if (_isInitialized && _monaco.getValue() != Value)
            {
                _monaco.setValue(Value);
            }
        }



        WebBrowser _browser;
        MonacoIntegration _monaco;
        private bool _isDisposed = false;
        bool _isInitialized;

        public MonacoEditor()
        {
            EmbeddedHttpServer.EnsureStarted();

            var sitePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonacoWpf", "Site");
            _isInitialized = false;
            _browser = new WebBrowser();
            var tb = new TextBlock
            {
                Text = "Loading Editor...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _monaco = new MonacoIntegration(
                browser: _browser,
                onValueChanged: value => Value = value,
                getValue: () => Value,
                getLang: () => _lang,
                log: (s,m) => {
                },
                onInitDone: () =>
                {
                    if (_isDisposed)
                        return;

                    _isInitialized = true;
                    foreach (var a in _afterInits)
                    {
                        a();
                    }
                    UpdateValue();
                    var e = OnEditorInitialized;
                    if(e != null)
                    {
                        e.Invoke(this, new EventArgs());
                    }
                    _browser.Visibility = Visibility.Visible;
                    tb.Visibility = Visibility.Collapsed;

                });
            _browser.ObjectForScripting = _monaco;

            this.Loaded += (o, e) =>
            {

            };
            this.Unloaded += (o, e) =>
            {
                _isDisposed = true;
                _browser.Dispose();
            };

          
            _browser.Visibility = Visibility.Collapsed;
            Content = new Grid
            {
                Children =
                {
                    tb,
                    _browser
                }
            };

            _browser.Navigate(EmbeddedHttpServer.EditorUri);
        }

        public void RegisterJsonSchema(string schema)
        {

            if (_isInitialized && !_isDisposed)
            {
                _monaco.registerJsonSchema(schema);
            }
            else
            {
                _afterInits.Add(() => _monaco.registerJsonSchema(schema));
            }
        }

        List<IRequestHandler> _handlers = new List<IRequestHandler>();
        List<Action> _afterInits = new List<Action>();
        public void RegisterCSharpServices(Guid id, IRequestHandler handler)
        {
            _handlers.Add(handler);
            EmbeddedHttpServer.AddHandler(handler);
            if (_isInitialized && !_isDisposed)
            {
                _monaco.registerCSharpsServices(id);
            }
            else
            {
                _afterInits.Add(() => _monaco.registerCSharpsServices(id));
            }
        }
        public void RemoveCSharpServices(Guid id, IRequestHandler handler)
        {
            _handlers.Remove(handler);
            EmbeddedHttpServer.RemoveHandler(handler);
        }

        public void InvokeScript(string name, params object[] args)
        {
            Action invoke = () =>
                _browser.InvokeScript(name, args);
                

            if (_isInitialized && !_isDisposed)
                invoke();
            else
                _afterInits.Add(invoke);
        }

        private string _lang = "plaintext";
        public void SetLanguage(string id)
        {
            _lang = id;
            InvokeScript("editorSetLang", id);
        }

        // Major credit to op0x59 on his implementation of Monaco
        public void SetTheme(string theme) =>
            InvokeScript("SetTheme", theme);


        public void AddIntellisense(string label, string type, string description, string insert) =>
            InvokeScript("AddIntellisense", new object[] {label, type, description, insert});


        public void ShowSyntaxError(int line, int column, int endLine, int endColumn, string message) =>
            InvokeScript("ShowErr", new object[] { line, column, endLine, endColumn, message });


        public void SetScroll(int lineNumber) =>
            InvokeScript("SetScroll", new object[] { lineNumber });

        public void UpdateSettings(MonacoEditorSettings settings)
        {
            InvokeScript("SwitchMinimap", new object[] { settings.MinimapEnabled });
            InvokeScript("SwitchReadonly", new object[] { settings.ReadOnly });
            InvokeScript("SwitchRenderWhitespace", new object[] { settings.RenderWhitespace });
            InvokeScript("SwitchLinks", new object[] { settings.Links });
            InvokeScript("SwitchLineHeight", new object[] { settings.LineHeight });
            InvokeScript("SwitchFontSize", new object[] { settings.FontSize });
            InvokeScript("SwitchFolding", new object[] { settings.Folding });
            InvokeScript("SwitchAutoIndent", new object[] { settings.AutoIndent });
            InvokeScript("SwitchFontFamily", new object[] { settings.FontFamily });
            InvokeScript("SwitchFontLigatures", new object[] { settings.FontLigatures });
        }

        public List<EditorLanguage> GetEditorLanguages()
        {
            if (_isDisposed)
                return null;

            if (_isInitialized && !_isDisposed)
            {
                var langs = _monaco.editorGetLanguages();

                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<EditorLanguage>>(langs);
            }
            throw new ArgumentException();
        }


    }

}
