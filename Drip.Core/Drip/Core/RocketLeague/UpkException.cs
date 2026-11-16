using System;

namespace Drip.Core.RocketLeague;

public sealed class UpkException : Exception
{
	public UpkException(string message)
		: base(message)
	{
	}
}
