// See https://aka.ms/new-console-template for more information
using StopBudoShuren;

Console.WriteLine("Hello, World!");
Console.WriteLine("Press Enter to Stop BudoShuren at the following Address:");
Console.WriteLine();
Console.WriteLine(Doer.Url);
Console.WriteLine();

Console.ReadLine();


Console.WriteLine("Stop gestartet");
Doer.StopApp();

Console.WriteLine("Press Enter to Exit");
Console.ReadLine();