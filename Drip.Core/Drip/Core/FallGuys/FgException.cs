using System;

namespace Drip.Core.FallGuys;

public sealed class FgException : Exception
{
	public FgException(string message)
		: base(message)
	{
	}
}
