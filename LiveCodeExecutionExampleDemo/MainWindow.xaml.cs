using System.IO;
using System.Runtime.Loader;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LiveCodeExecutionExampleDemo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private AppModel model;

    public MainWindow()
    {
        InitializeComponent();
        this.model = new AppModel(this.OutputLog);
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        // setup the wrapper code template.
        var generatedCode =
            "using LiveCodeExecutionExampleDemo;" + Environment.NewLine +
            "public class Plugin" + Environment.NewLine +
            "{" + Environment.NewLine +
            "   public void RunCode(AppModel app)" + Environment.NewLine +
            "   {" + Environment.NewLine +
            $"{this.CodeEditor.Text}" +
            "   }" + Environment.NewLine +
            "}" + Environment.NewLine;

        // parse the generated C# code
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
                 
        // Create a collection of references to the assemblies that the code depends on. We just give it everything this app domain has loaded.
        var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assembliesWithUncLocation = domainAssemblies.Where(x => !string.IsNullOrWhiteSpace(x.Location));
        var references = assembliesWithUncLocation.Select(x => MetadataReference.CreateFromFile(x.Location));

        // Create a C# compilation using the syntax tree and the list of references
        var compilation = CSharpCompilation.Create(
            "MyAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create a memory stream to hold the compiled assembly. Memory stream will be disposed after emitting the assembly as it is no longer needed.
        using (var stream = new MemoryStream())
        {
            // Compile the assembly and write it to the stream
            var result = compilation.Emit(stream);

            // Check for compilation errors
            if (result.Success)
            {
                // The assembly was successfully compiled, so you can use it in your application. But remember to seek ot the beginning of the stream first
                stream.Seek(0, SeekOrigin.Begin);
                this.RunPlugin(stream);
            }
            else
            {
                // There were compilation errors, so you will need to fix them before you can use the assembly
                foreach (var diagnostic in result.Diagnostics)
                {
                    this.OutputLog.AppendText(diagnostic.ToString() + Environment.NewLine);
                }
            }
        }
    }

    private void RunPlugin(Stream stream)
    {
        // Load the plugin assembly into a separate load context to isolate it, that way we cal unload the context and loaded assemblies when we are done.
        var pluginContext = new AssemblyLoadContext("PluginContext", true);
        
        // load the assembly
        var assembly = pluginContext.LoadFromStream(stream);

        //  finds the plugin type
        var pluginType = assembly.GetExportedTypes().First(x => x.Name.Equals("Plugin"));
        
        // we wrap with a try catch so if something goes wrong we can report it to the log
        try
        {
            // create plugin and invoke the RunCode method passing in the app model reference
            var objInst = Activator.CreateInstance(pluginType);
            var method = pluginType.GetMethods().Where(x => x.IsPublic).FirstOrDefault(x => x.Name.Equals("RunCode"));

            method.Invoke(objInst, new[] { this.model });
        }
        catch (Exception ex)
        {
            // something went wrong so report it
            this.OutputLog.AppendText(ex.ToString() + Environment.NewLine);
        }

        // Unload the context and release the plugin
        pluginContext.Unload();
    }
}