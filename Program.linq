<Query Kind="Program">
  <Reference Relative="GameForm.linq">&lt;MyDocuments&gt;\LINQPad Queries\dot game\GameForm.linq</Reference>
</Query>

#load "MainMenuForm.linq"
#load "EditorForm.linq"
#load "GameForm.linq"
#load "Maps.linq"
#load "Characters.linq"


using System;
using System.Windows.Forms;

namespace DotGameCSharp
{
    internal static class Program
    {
        // Program.cs
[STAThread]
static void Main()
{
    Application.EnableVisualStyles();
    Application.Run(new MainMenuForm());
}

    }
}
