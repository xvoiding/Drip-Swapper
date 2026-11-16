using System;
using System.Collections.Generic;
using System.Linq;

namespace Drip.Core.Swapping;

public sealed class VerifyReport
{
	public List<VerifyLine> Lines { get; } = new List<VerifyLine>();

	public int Records { get; set; }

	public int Containers { get; set; }

	public bool Ok => Lines.All((VerifyLine l) => l.Level != VerifyLevel.Fail);

	public int Fails => Enumerable.Count(Lines, (VerifyLine l) => l.Level == VerifyLevel.Fail);

	public int Warns => Enumerable.Count(Lines, (VerifyLine l) => l.Level == VerifyLevel.Warn);

	public void Add(VerifyLevel level, string container, string text)
	{
		Lines.Add(new VerifyLine(level, container, text));
	}

	public string Summary()
	{
		if (Records != 0 || Fails != 0)
		{
			if (Ok)
			{
				return $"Verified {Records} swapped asset(s) in {Containers} container(s): the game files hold them{((Warns > 0) ? $" ({Warns} warning(s))" : "")}.";
			}
			return $"{Fails} problem(s) found in {Containers} container(s). See the details below.";
		}
		return "No swaps are recorded, so there is nothing to verify.";
	}

	public override string ToString()
	{
		return Summary() + Environment.NewLine + string.Join(Environment.NewLine, Lines);
	}
}
