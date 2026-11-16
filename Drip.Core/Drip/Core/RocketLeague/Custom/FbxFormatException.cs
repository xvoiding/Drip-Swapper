using System;

namespace Drip.Core.RocketLeague.Custom;

public sealed class FbxFormatException : Exception
{
	public FbxFormatException(string m)
		: base(m)
	{
	}
}
