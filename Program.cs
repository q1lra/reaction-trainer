using System;
using System.Windows.Forms;

namespace ReactionTrainer; // Updated namespace

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}