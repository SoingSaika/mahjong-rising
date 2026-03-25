using System;
using System.Collections.Generic;
using System.Linq;

namespace MahjongRising.code.Character;

/// <summary>
/// 角色注册中心。
/// 管理角色定义和角色能力。Mod 可注册全新角色。
/// </summary>
public sealed class CharacterRegistry
{
    private readonly Dictionary<string, CharacterDefinition> _characters = new();
    private readonly Dictionary<string, ICharacterAbility> _abilities = new();

    // ── 角色注册 ──

    public void RegisterCharacter(CharacterDefinition character)
    {
        _characters[character.CharacterId] = character;
    }

    public bool UnregisterCharacter(string characterId)
    {
        return _characters.Remove(characterId);
    }

    public CharacterDefinition? GetCharacter(string characterId)
    {
        _characters.TryGetValue(characterId, out var ch);
        return ch;
    }

    public IReadOnlyList<CharacterDefinition> GetAllCharacters()
        => _characters.Values.ToList();

    public IReadOnlyList<CharacterDefinition> GetCharactersByRarity(string rarity)
        => _characters.Values.Where(c => c.Rarity == rarity).ToList();

    // ── 能力注册 ──

    public void RegisterAbility(ICharacterAbility ability)
    {
        _abilities[ability.AbilityId] = ability;
    }

    public bool UnregisterAbility(string abilityId)
    {
        return _abilities.Remove(abilityId);
    }

    public ICharacterAbility? GetAbility(string abilityId)
    {
        _abilities.TryGetValue(abilityId, out var ability);
        return ability;
    }

    /// <summary>获取某角色的所有能力实例。</summary>
    public List<ICharacterAbility> GetAbilitiesForCharacter(CharacterDefinition character)
    {
        var result = new List<ICharacterAbility>();
        foreach (var id in character.AbilityIds)
        {
            if (_abilities.TryGetValue(id, out var ability))
                result.Add(ability);
        }
        return result;
    }

    public IReadOnlyList<ICharacterAbility> GetAllAbilities()
        => _abilities.Values.ToList();
}
