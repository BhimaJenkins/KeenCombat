using UnityEngine;

namespace KeenCombat
{
    // -----------------------------------------------------------------------
    // SkillMasks
    //
    // Valheim puts characters on different physics layers depending on which
    // client controls them:
    //   character       — controlled by this client
    //   character_net   — controlled by ANOTHER player's client
    //   character_ghost / character_noenv — special cases
    //
    // Searching only "character" misses every enemy (and player) controlled
    // by someone else in multiplayer. Always use SkillMasks.Characters for
    // area searches (OverlapSphere, OverlapCapsule, etc).
    // -----------------------------------------------------------------------
    public static class SkillMasks
    {
        private static int _characters = -1;

        public static int Characters
        {
            get
            {
                if (_characters == -1)
                {
                    _characters = LayerMask.GetMask(
                        "character", "character_net", "character_ghost", "character_noenv");
                }
                return _characters;
            }
        }
    }
}