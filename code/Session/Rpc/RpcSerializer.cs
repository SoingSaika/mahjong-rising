using System.Linq;
using System.Text.Json;
using MahjongRising.code.Game.Rules;
using MahjongRising.code.Game.Rules.Validators;
using MahjongRising.code.Mahjong.States;
using MahjongRising.code.Player.States;

namespace MahjongRising.code.Session.Rpc;

/// <summary>
/// RPC 序列化工具。
/// Godot RPC 传参只支持基本类型和 string，
/// 所以将 DTO 序列化为 JSON string 传输。
/// </summary>
public static class RpcSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, JsonOpts);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOpts);

    // ── 领域对象 → DTO 转换 ──

    public static TileDto ToDto(MahjongTileState tile, bool reveal = true)
    {
        return new TileDto
        {
            InstanceId = tile.InstanceId.ToString(),
            TileCode = reveal ? tile.Tile.TileCode : "hidden",
            IsFaceUp = reveal && tile.IsFaceUp
        };
    }

    public static MeldDto ToDto(MeldGroup meld)
    {
        return new MeldDto
        {
            Kind = meld.Kind,
            Tiles = meld.Tiles.Select(t => ToDto(t, meld.IsOpen)).ToList(),
            SourceSeat = meld.SourceSeat,
            IsOpen = meld.IsOpen
        };
    }

    public static AvailableActionsDto ToDto(AvailableActions actions)
    {
        return new AvailableActionsDto
        {
            Seat = actions.Seat,
            Chi = actions.Chi?.Select(ToOptionDto).ToList(),
            Peng = actions.Peng?.Select(ToOptionDto).ToList(),
            Gang = actions.Gang?.Select(ToOptionDto).ToList(),
            Hu = actions.Hu?.Select(ToOptionDto).ToList(),
            Custom = actions.Custom.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(ToOptionDto).ToList())
        };
    }

    private static ActionOptionDto ToOptionDto(ActionOption opt)
    {
        return new ActionOptionDto
        {
            OptionId = opt.OptionId,
            InvolvedTileIds = opt.InvolvedTileIds.Select(id => id.ToString()).ToList(),
            Extra = opt.Extra.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? "")
        };
    }
}
