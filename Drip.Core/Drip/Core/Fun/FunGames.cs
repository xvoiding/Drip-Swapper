namespace Drip.Core.Fun;

public static class FunGames
{
	public const string Fortnite = "fortnite";

	public const string RocketLeague = "rocketleague";

	public const string FallGuys = "fallguys";

	public static readonly string[] All = new string[3] { "fortnite", "rocketleague", "fallguys" };

	public static string Pretty(string g)
	{
		return g switch
		{
			"fortnite" => "Fortnite", 
			"rocketleague" => "Rocket League", 
			"fallguys" => "Fall Guys", 
			_ => g, 
		};
	}
}
