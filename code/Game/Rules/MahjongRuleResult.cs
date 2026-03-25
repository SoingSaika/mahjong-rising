using System.Collections.Generic;

namespace MahjongRising.code.Game.Rules;

public class MahjongRuleResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = "";

    public List<string> Logs { get; } = new();

    public static MahjongRuleResult Ok(string message = "")
    {
        return new MahjongRuleResult
        {
            Success = true,
            Message = message
        };
    }

    public static MahjongRuleResult Fail(string message)
    {
        return new MahjongRuleResult
        {
            Success = false,
            Message = message
        };
    }
}