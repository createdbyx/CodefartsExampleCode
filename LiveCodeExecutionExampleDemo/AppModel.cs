using System.Windows.Controls;

namespace LiveCodeExecutionExampleDemo;

public class AppModel
{
    private readonly TextBox log;

    public AppModel(TextBox outputLog)
    {
        this.log = outputLog ?? throw new ArgumentNullException(nameof(outputLog));
    }

    public void Write(string text)
    {
        this.log.AppendText(text + Environment.NewLine);
    }
}