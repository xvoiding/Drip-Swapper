using System;
using System.Diagnostics;
using System.Windows;

namespace Drip.App;

public partial class App : Application
{
	public const string DiscordUrl = "https://discord.gg/pQEHJPAmnC";

	public static void OpenDiscord()
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "https://discord.gg/pQEHJPAmnC",
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private static void LogCrash(string where, Exception? ex)
	{
		try
		{
			string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Drip Swapper");
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), DateTime.Now.ToString("s") + "  " + where + "\n" + (ex?.ToString() ?? "(no details)") + "\n\n");
		}
		catch
		{
		}
	}

	private void App_Startup(object sender, StartupEventArgs e)
	{
		DispatcherUnhandledException += (object _, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs a) =>
		{
			LogCrash("ui", a.Exception);
			a.Handled = true;
			try
			{
				MessageBox.Show("Something went wrong: " + a.Exception.Message + "\n\nDetails were saved to %LocalAppData%\\Drip Swapper\\crash.log. Send that file to the Drip Swapper Discord if it keeps happening.", "Drip Swapper");
			}
			catch
			{
			}
		};
		AppDomain.CurrentDomain.UnhandledException += (object _, UnhandledExceptionEventArgs a) => LogCrash("fatal", a.ExceptionObject as Exception);
		System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (object? _, System.Threading.Tasks.UnobservedTaskExceptionEventArgs a) =>
		{
			LogCrash("task", a.Exception);
			a.SetObserved();
		};
		Localizer.Install();
		Theme.Load();
		new GameChooserWindow().Show();
	}

	public static void OpenChooser(Window from)
	{
		GameChooserWindow gameChooserWindow = new GameChooserWindow
		{
			WindowStartupLocation = WindowStartupLocation.Manual
		};
		gameChooserWindow.Left = from.Left + (from.Width - gameChooserWindow.Width) / 2.0;
		gameChooserWindow.Top = from.Top + (from.Height - gameChooserWindow.Height) / 2.0;
		Application.Current.MainWindow = gameChooserWindow;
		gameChooserWindow.Show();
		from.Close();
	}

	public static void Open(Game game, Window? from)
	{
		Window window = game switch
		{
			Game.RocketLeague => (Window)new RocketLeagueWindow(), 
			Game.FallGuys => new FallGuysWindow(), 
			Game.Retrac => new MainWindow(retrac: true), 
			Game.Stellar => new StellarWindow(), 
			_ => new MainWindow(), 
		};
		if (from != null)
		{
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Left = from.Left + (from.Width - window.Width) / 2.0;
			window.Top = from.Top + (from.Height - window.Height) / 2.0;
		}
		Application.Current.MainWindow = window;
		window.Show();
		from?.Close();
	}
}
