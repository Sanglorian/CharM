# Orcus content audit (machine-generated)

- Source books scanned: 4
- YAML elements loaded: 1597
- Prose fields scanned: 4100; flagged: 602
- Flavor fields: 401 faithful, 12 INVENTED

A field is flagged when its text (after stripping markdown, smart quotes,
bullets, punctuation, case and whitespace) is **not found verbatim** in any
source book. Such text was reworded, fabricated, or otherwise altered.

## Invented Flavor fields (not in any source) — to be removed

- ancestry.yaml :: Violent Rush :: "You press the advantage before the enemy can recover."
- classes/commander.yaml :: Commander :: "Battles are won by those who can make others fight as one."
- classes/exemplar.yaml :: Exemplar :: "You are a duelist who turns momentum into a killing edge."
- classes/guardian.yaml :: Guardian :: "You stand where the line must hold, and you do not move."
- classes/harlequin.yaml :: Harlequin :: "A trickster who controls the battlefield with auras, taunts and misdirection."
- classes/mageblade.yaml :: Mageblade :: "A warrior-mage who binds enemies with an arcane sigil and strikes with elemental steel."
- classes/magician.yaml :: Magician :: "You bend the elements and the minds of others to your will."
- classes/priest.yaml :: Priest :: "You channel the will of the gods to mend, ward and rally."
- classes/reaper.yaml :: Reaper :: "You loose rains of arrows and bind the battlefield with willing spirits."
- classes/sylvan.yaml :: Sylvan :: "You wear the shapes of beasts and run with a companion at your side."
- paths/epic.yaml :: For the Sake of the Team :: "You give an ally the precious time they need to complete the task."
- paths/epic.yaml :: Not Tolerate Losing :: "You make every ally fear you more than the enemy."

## Fidelity flags — reworded / fabricated prose, by file

### ancestries-species.yaml
```
  [Race] Apefolk — field 'Description':
      ✗ Humanoids reminiscent of gorillas, orang-utans and chimpanzees, apefolk prize legacy and enduring might
  [Race] Automaton — field 'Description':
      ✗ Created beings of brass, clockwork and a pinch of sorcery, the automatons are a relatively new people still finding their place in the world
  [Race] Azer — field 'Description':
      ✗ Stout beings who resemble dwarves with hair of fire, azer are often skilled artisans organised on quasi-military lines
  [Race] Catfolk — field 'Description':
      ✗ Playful, lithe humanoids with feline heads and short fur, different catfolk clans resemble different breeds of big cat
  [Race] Deepfolk — field 'Description':
      ✗ Amphibious humanoids with fish-like features, deepfolk are skilled in underwater combat and wield weapons of coral and obsidian
  [Power] Blinding Mucus — field 'Attack':
      ✗ Dexterity, Wisdom or Charisma (your choice) vs Reflex
  [Race] Dromite — field 'Description':
      ✗ Small insect-like humanoids who form cooperative colonies in great earthen mounds
      ✗ their powerful legs let them leap great distances
  [Race] Frogfolk — field 'Description':
      ✗ Amphibious humanoids with webbed hands, sticky tongues and a cheery, adventurous streak that sours fast if their skin dries out
  [Race] Gnoll — field 'Description':
      ✗ Humanoid hyenas known for wildness and cunning, fiercely loyal to their pack but with little patience for fools
  [Race] Half-Giant — field 'Description':
      ✗ Seven- to eight-foot-tall humanoids who are their own people (not the offspring of giants), proud and inclined to see themselves as leaders
  [Power] Stomp — field 'Attack':
      ✗ Strength, Constitution or Wisdom (your choice) vs Fortitude
  [Race] Hobgoblin — field 'Description':
      ✗ Bright-skinned humanoids with pointed ears and heavy brows, known for discipline, honour and rigid hierarchical societies
  [Race] Mephit — field 'Description':
      ✗ Small, winged imps from the elemental planes - mischievous, clever and talkative, delighting in their associated element
  [Power] Breath Weapon — field 'Target':
      ✗ Near arc 3, all targets
  [Power] Breath Weapon — field 'Attack':
      ✗ Highest of Strength, Constitution or Dexterity vs Reflex
  [Race] Minotaur — field 'Description':
      ✗ Strong, imposing half-bull half-humans, marked by courage and a struggle to reconcile fierce instincts with human emotion
  [Power] Momentum Blow — field 'Requirements':
      ✗ You have made an attack after moving at least two squares this turn
  [Race] Shadow Elf — field 'Description':
      ✗ Enigmatic descendants of refugees from the Seelie-Unseelie wars, shadow elves are known for haunting beauty and a taste for secrecy and intrigue
  [Race] Vishya — field 'Description':
      ✗ Graceful humanoids with venomous fangs, serpentine eyes and supple, lightly scaled skin often patterned like a serpent's
```
### ancestry.yaml
```
  [Race] Humanity — field 'Description':
      ✗ The default Orcus ancestry: heroes defined by the moment that marked them (their crux) and the culture they came from (their heritage), rather than by species
  [Power] Ingenuity — field 'Effect':
      ✗ Power bonuses expire at the end of your next turn if not used
  [Crux] Escaped — field 'Description':
      ✗ You were imprisoned, trapped or otherwise doomed - but you somehow slipped away
  [Heritage] Heretic — field 'Description':
      ✗ Your family belonged to a forbidden religion, exposing you to constant persecution
  [Power] Barrel Along — field 'Effect':
      ✗ You gain a +2 bonus to speed and to damage rolls until the end of the encounter
```
### classes/commander.yaml
```
  [Class Feature] Stormtrooper Tactics — field 'Description':
      ✗ Once per turn, an ally targeted by one of your powers can shift 1 as a free action
  [Class Feature] Siege Tactics — field 'Description':
      ✗ Once per turn, an ally targeted by one of your powers gains temporary hit points equal to your Wisdom modifier
  [Class Feature] Resilience Tactics — field 'Description':
      ✗ Once per turn, an ally targeted by one of your powers makes a saving throw against one save-ends condition
  [Class Feature] Inspiring Tactics — field 'Description':
      ✗ Once per turn, an ally targeted by one of your powers gains a +2 power bonus to their next attack roll
  [Class Feature] Armament: Martial Ranged — field 'Description':
      ✗ you may use Strength instead of Dexterity for basic ranged attacks
```
### classes/exemplar.yaml
```
  [Class Feature] Momentum — field 'Description':
      ✗ When you hit an enemy with a melee attack and don't already have momentum, you gain momentum
      ✗ You lose it when you are hit by any attack
      ✗ Momentum has no effect on its own but is required by Triumphant Strike and Bide Your Time and referenced by some powers
  [Class Feature] Triumphant Strike — field 'Description':
      ✗ While you have momentum, once per turn you can add +1dW damage to any attack
  [Class Feature] Bide Your Time — field 'Description':
      ✗ If you use an encounter attack power and hit no targets, you can lose your momentum to keep the power (it is not expended)
  [Class Feature] Nick of Time — field 'Description':
      ✗ You get a +2 bonus on attack rolls you make outside of your turn (opportunity attacks, immediate actions)
  [Class Feature] Swashbuckler — field 'Description':
      ✗ Your Triumphant Strike deals additional damage equal to your Charisma modifier against a marked enemy
```
### classes/harlequin.yaml
```
  [Class Feature] Fixer — field 'Description':
      ✗ Demoralizing Presence aura 2: enemies in the aura take a -2 penalty to all saving throws
  [Class Feature] Jester — field 'Description':
      ✗ Attention Grabber aura 2: you can mark an enemy in the aura as a swift action
      ✗ enemies you marked inside the aura take an extra -1 to attacks that don't include you
```
### classes/mageblade.yaml
```
  [Class Feature] Illusion Specialist — field 'Description':
      ✗ You gain the blurring sigil power
  [Class Feature] Martyr Specialist — field 'Description':
      ✗ You gain the vortex sigil power
  [Class Feature] Punishment Specialist — field 'Description':
      ✗ You gain the fiery sigil power
  [Class Feature] Rush Specialist — field 'Description':
      ✗ You gain the beacon sigil power
```
### classes/magician.yaml
```
  [Class Feature] Cantrips — field 'Description':
      ✗ You gain the Cantrip Master feat (three powers from the Cantrips discipline)
  [Class Feature] Conjurer — field 'Description':
      ✗ Active daily Summon powers are not expended when an encounter ends (re-summoning restores the creature's prior state but refreshes its encounter powers)
  [Class Feature] Enchanter — field 'Description':
      ✗ When a Psychic power would deal damage, you may instead deal none to your enemy targets for a +2 bonus on the attack roll
  [Class Feature] Evoker — field 'Description':
      ✗ Increase the area of a near/far Acid, Cold, Fire, Flux, Lightning or Thunder power by 1
  [Class Feature] Arcane Overflow — field 'Description':
      ✗ Your class encounter attack powers gain a Miss entry if they lack one: half damage, and a hit's condition is downgraded (e
      ✗ stunned -> dazed, immobile -> slowed)
  [Class Feature] Arcane Sustenance — field 'Description':
      ✗ At the start of each turn, freely Maintain one active power or give one of your companions an action (without spending your own)
```
### classes/priest.yaml
```
  [Class Feature] Worships the God of Life — field 'Description':
      ✗ Your healing powers are bolstered by the god of life
  [Class Feature] Worships the God of Peace — field 'Description':
      ✗ You ward and protect your allies
  [Class Feature] Worships the God of Tyranny — field 'Description':
      ✗ You command through fear and dominion
  [Class Feature] Worships the God of War — field 'Description':
      ✗ You lead the faithful into battle
```
### classes/reaper.yaml
```
  [Class Feature] Spirit Entreaty — field 'Description':
      ✗ Once per encounter, entreat the spirits for one of: grasping vines, spirit's prank or unleashed spirit
      ✗ (Action Recharge can refresh it
  [Power] Sprouting Overwatch — field 'Effect':
      ✗ Until the start of your next turn, make a ranged basic attack (immediate interrupt) against a creature that enters the area
      ✗ afterward enemies treat the area as difficult terrain until the start of your next turn
  [Power] Grasping Vines — field 'Effect':
      ✗ Level 11: burst 2
      ✗ Level 21: burst 3
  [Class Feature] Paviser — field 'Description':
      ✗ If you don't move on your turn, reduce unwilling movement by 1 and gain +1 AC until your next turn
      ✗ Proficient with hide and chainmail
  [Class Feature] Peltast — field 'Description':
      ✗ +1 to attacks with thrown weapons, which return to your hand
      ✗ In light or no armor, add Strength to AC instead of Dex/Int if higher
  [Class Feature] Sharpshooter — field 'Description':
      ✗ +1 to attack rolls against targets 6 or more squares away
```
### classes/sylvan.yaml
```
  [Power] Fearful Rampage — field 'Effect':
      ✗ The target takes 2 + your Wisdom modifier damage and is shunted 1
      ✗ Level 11: one or two creatures
      ✗ Level 21: all enemies
  [Power] Companion Rampage — field 'Effect':
      ✗ The target (within melee reach of you or your animal companion) takes 2 + your Charisma modifier damage
      ✗ Level 11: one or two creatures
      ✗ Level 21: all enemies
  [Power] Swift Rampage — field 'Effect':
      ✗ The target takes 2 + your Dexterity modifier damage
      ✗ Level 11: one or two creatures
      ✗ Level 21: all enemies
  [Class Feature] Wild Gift: Skinchanger — field 'Description':
      ✗ In light or no armor, add your Constitution modifier to AC instead of Dexterity/Intelligence
      ✗ You can spend additional shape powers for extra Form Attacks
  [Class Feature] Wild Gift: Hunter — field 'Description':
      ✗ +1 to attacks vs enemies no ally is closer to
  [Class Feature] Wild Gift: Animal Companion — field 'Description':
      ✗ You gain an animal companion (its level = yours
      ✗ When you use a Red in Tooth and Claw power, your companion may use it in your place
```
### companions.yaml
```
  [Companion] Ape — field 'Attack':
      ✗ Stone (ranged 5/10): your level +4 vs AC, 1d8 + your level
  [Companion] Ape — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ Could also be a yeti
  [Companion] Arboreal Sapling — field 'Attack':
      ✗ Branch (standard, at-will): your level +3 vs AC, 1d10 + your level
  [Companion] Arboreal Sapling — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ Could also be a wood golem
  [Companion] Bear — field 'Description':
      ✗ Sturdy: starts each battle with temporary HP equal to double its level
      ✗ AC/defenses are the listed value + your level
  [Companion] Big Cat — field 'Description':
      ✗ Greased Lightning: on the first round, combat advantage against any creature that has not yet acted
      ✗ AC/defenses are the listed value + your level
      ✗ A lion, tiger, jaguar, leopard or cougar
  [Companion] Blink Dog — field 'Description':
      ✗ Jolt Back: after being hit by an attack, it can teleport 2
      ✗ AC/defenses are the listed value + your level
  [Companion] Bull — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ A bull, buffalo, cow, yak or boar
  [Companion] Giant Bat — field 'Description':
      ✗ AC/defenses are the listed value + your level
  [Companion] Giant Lizard — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ A cave gecko, giant iguana
  [Companion] Giant Raptor — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ A hawk, eagle, owl or falcon
  [Companion] Giant Snake — field 'Description':
      ✗ Venomous Snap: opportunity attacks also deal 2 persistent poison (save ends)
      ✗ 4 at level 11, 6 at level 21
      ✗ AC/defenses are the listed value + your level
      ✗ A venomous snake, spider, toad or Gila monster
  [Companion] Horse — field 'Description':
      ✗ AC/defenses are the listed value + your level
  [Companion] Hound — field 'Description':
      ✗ Combat Advantage: +2 damage when it has combat advantage
      ✗ +4 at level 11, +6 at level 21
      ✗ AC/defenses are the listed value + your level
  [Companion] Hunting Spider — field 'Description':
      ✗ AC/defenses are the listed value + your level
      ✗ A web-throwing spider, chameleon or giant frog
```
### deities.yaml
```
  [Deity] God of War — field 'Description':
      ✗ Worshippers gain the Art of War discipline and martial blessings
  [Deity] God of Peace — field 'Description':
      ✗ Worshippers ward and shield their allies
  [Deity] God of Life — field 'Description':
      ✗ Worshippers bolster their healing and guard the dying
  [Deity] God of Tyranny — field 'Description':
      ✗ Worshippers cow their foes and drive their allies on
```
### equipment/gear.yaml
```
  [Focus] Holy Symbol — field 'Description':
      ✗ Channels divine powers with the Focus tag
  [Focus] Martial Focus — field 'Description':
      ✗ An item that inspires or focuses the mind for battle (a banner, a book of strategies, hand wraps)
      ✗ Channels martial powers with the Focus tag
  [Focus] Arcane Focus (Orb) — field 'Description':
      ✗ Channels arcane powers with the Focus tag
  [Focus] Arcane Focus (Wand) — field 'Description':
      ✗ Channels arcane powers with the Focus tag
```
### equipment/magic-items-boosts.yaml
```
  [Magic Item] +1 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +2 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +3 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +4 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +5 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +6 blessed focus — field 'Property':
      ✗ once per day you can use that Channel Divinity power
  [Magic Item] +1 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +2 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +3 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +4 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +5 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +6 brilliant focus — field 'Property':
      ✗ Item Power (free, encounter): when you hit, the target is stunned (save ends)
  [Magic Item] +1 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +2 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +3 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +4 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +5 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +6 courageous focus — field 'Property':
      ✗ they gain +4 to their next attack roll
  [Magic Item] +5 dismissal focus — field 'Property':
      ✗ the target is teleported to a pocket dimension until the end of your next turn (cannot move or see)
      ✗ sustain standard once
  [Magic Item] +6 dismissal focus — field 'Property':
      ✗ the target is teleported to a pocket dimension until the end of your next turn (cannot move or see)
      ✗ sustain standard once
  [Magic Item] +1 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +2 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +3 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +4 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +5 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +6 draining focus — field 'Property':
      ✗ Item Power (swift, encounter) Necrotic: until the end of your next turn, each hit with this focus does +2d6 necrotic but you take 1d6 damage
  [Magic Item] +1 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +2 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +3 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +4 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +5 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +6 elemental's ally focus — field 'Property':
      ✗ Item Power (swift, daily): Near burst 5, one target gains vulnerability 5 to acid, fire, lightning or cold (10 at L12, 15 at L22)
  [Magic Item] +1 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +2 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +3 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +4 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +5 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +6 energy absorbing focus — field 'Property':
      ✗ Item Power (counter, daily) Focus: when an enemy within 10 is about to recharge a power, Charisma vs Will
      ✗ it does not recharge and you regain an encounter power
  [Magic Item] +1 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +2 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +3 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +4 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +5 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +6 finisher focus — field 'Property':
      ✗ You do an additional +X damage to staggered creatures
  [Magic Item] +1 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +2 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +3 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +4 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +5 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +6 forceful focus — field 'Property':
      ✗ When you make a creature move unwillingly, increase the distance by 1 square
  [Magic Item] +1 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +2 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +3 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +4 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +5 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +6 grounded focus — field 'Property':
      ✗ +2 enhancement bonus on saving throws against effects with this focus's chosen tags
  [Magic Item] +1 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +2 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +3 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +4 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +5 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +6 mana battery focus — field 'Property':
      ✗ Item Power (swift, daily): recover an expended arcane encounter power of the item's level or lower
  [Magic Item] +1 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +2 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +3 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +4 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +5 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +6 reshaping focus — field 'Property':
      ✗ Item Power (free, encounter): change a power's damage to this focus's associated damage type
  [Magic Item] +1 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +2 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +3 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +4 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +5 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +6 runic focus — field 'Property':
      ✗ Once per day, perform an incantation of the focus's level or lower that you do not know (you still pay components)
  [Magic Item] +1 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +2 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +3 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +4 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +5 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +6 sapping focus — field 'Property':
      ✗ Item Power (free, encounter): on a hit, one target suffers -2 to saving throws until the end of its next turn
  [Magic Item] +1 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +2 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +3 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +4 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +5 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +6 versatile focus — field 'Property':
      ✗ Counts as all varieties of focus (arcane, druidic, holy symbol and martial)
  [Magic Item] +1 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +2 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +3 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +4 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +5 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +6 warlike focus — field 'Property':
      ✗ This focus also carries a weapon boost (GM's choice)
  [Magic Item] +1 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +2 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +3 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +4 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +5 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +6 bleeding weapon — field 'Property':
      ✗ Stores blood points (enhancement x5)
      ✗ Absorb Blood (free, enc): gain blood points equal to damage dealt
      ✗ Unleash Blood (free, enc): on a hit, deal extra damage equal to stored blood points, then reset
  [Magic Item] +1 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +2 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +3 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +4 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +5 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +6 disruption weapon — field 'Property':
      ✗ +Xd6 damage vs Demon/Devil/Undead
      ✗ staggering such a creature rattles it
      ✗ Sheds bright light (4) and dim light (4 more)
  [Magic Item] +1 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +2 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +3 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +4 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +5 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +6 dwarven thrower — field 'Property':
      ✗ +Xd6 damage vs Giant creatures
      ✗ Item Power (free, daily): on a hit, the target falls prone
  [Magic Item] +1 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +2 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +3 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +4 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +5 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +6 flame tongue weapon — field 'Property':
      ✗ Item Power (swift, encounter) Fire: bright light 8
      ✗ the first hit deals +Xd6 fire damage, then the effect ends
  [Magic Item] +1 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +2 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +3 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +4 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +5 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +6 frost brand weapon — field 'Property':
      ✗ On a hit, +1d6 cold damage
      ✗ While held, resistance to fire equal to double the enhancement
      ✗ Item Power (free, enc): on draw, extinguish nonmagical flames in burst 6
  [Magic Item] +1 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +2 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +3 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +4 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +5 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +6 hammer of thunderbolts — field 'Property':
      ✗ Item Power (free, daily) Thunder: reach 12 for the attack
      ✗ on a hit, a thunderclap — Near burst 3, Strength vs Fortitude, dazed (EONT)
  [Magic Item] +1 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +2 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +3 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +4 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +5 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +6 infectious mark weapon — field 'Property':
      ✗ Item Power (free, encounter): when a marked enemy drops to 0 HP, mark a new target within 5 squares
  [Magic Item] +1 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +2 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +3 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +4 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +5 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +6 lightning weapon — field 'Property':
      ✗ Item Power (standard, daily) Lightning: Near wall 24, Dexterity vs Reflex, Xd6 lightning damage
  [Magic Item] +1 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +2 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +3 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +4 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +5 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +6 luck blade — field 'Property':
      ✗ Item Power (free, daily): reroll one attack roll, check or save and take the second result
  [Magic Item] +1 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +2 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +3 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +4 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +5 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +6 slayer weapon — field 'Property':
      ✗ +Xd6 damage vs creatures of this weapon's attuned tag
  [Magic Item] +4 sworn vengeance weapon — field 'Property':
      ✗ Item Power (swift, daily): name a sworn enemy — combat advantage and no range/cover/concealment penalties vs it
      ✗ crit does +Xd12
      ✗ -2 to attacks with other weapons while it lives
  [Magic Item] +5 sworn vengeance weapon — field 'Property':
      ✗ Item Power (swift, daily): name a sworn enemy — combat advantage and no range/cover/concealment penalties vs it
      ✗ crit does +Xd12
      ✗ -2 to attacks with other weapons while it lives
  [Magic Item] +6 sworn vengeance weapon — field 'Property':
      ✗ Item Power (swift, daily): name a sworn enemy — combat advantage and no range/cover/concealment penalties vs it
      ✗ crit does +Xd12
      ✗ -2 to attacks with other weapons while it lives
  [Magic Item] +1 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +2 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +3 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +4 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +5 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +6 venom weapon — field 'Property':
      ✗ Item Power (swift, daily) Poison: coat the blade
      ✗ the next hit deals persistent poison (save ends) equal to double the enhancement
  [Magic Item] +1 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +2 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +3 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +4 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +5 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +6 warded weapon — field 'Property':
      ✗ Ranged and far attacks with this weapon do not provoke opportunity attacks
  [Magic Item] +1 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +2 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +3 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +4 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +5 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +6 amulet of proof against detection and location — field 'Property':
      ✗ Hidden from scrying magic
      ✗ cannot be targeted or perceived by magical scrying sensors
  [Magic Item] +5 amulet of the planes — field 'Property':
      ✗ Item Power (standard, at-will) Teleportation: attempt a plane walk via DC 15 Arcana
      ✗ on a failure you travel somewhere random
  [Magic Item] +6 amulet of the planes — field 'Property':
      ✗ Item Power (standard, at-will) Teleportation: attempt a plane walk via DC 15 Arcana
      ✗ on a failure you travel somewhere random
  [Magic Item] +1 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +2 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +3 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +4 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +5 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +6 amulet of rescue — field 'Property':
      ✗ Item Power (counter, daily) Healing: when reduced to 0 HP or below, spend a recovery and heal your recovery value
  [Magic Item] +1 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +2 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +3 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +4 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +5 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +6 amulet of shielding — field 'Property':
      ✗ Resistance to force equal to double the enhancement bonus
  [Magic Item] +1 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +2 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +3 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +4 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +5 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +6 amulet of up-and-down — field 'Property':
      ✗ When you would fail a death save, you may lose a recovery instead (if you have one)
  [Magic Item] +1 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +2 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +3 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +4 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +5 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +6 cape of the mountebank — field 'Property':
      ✗ Item Power (standard, daily) Illusion, Teleportation: teleport up to 20
  [Magic Item] +1 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +2 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +3 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +4 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +5 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +6 cloak of arachnida — field 'Property':
      ✗ Resistance to poison = double the enhancement
      ✗ climb speed = walk speed (wall-climber, no hands)
  [Magic Item] +1 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +2 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +3 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +4 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +5 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +6 cloak of displacement — field 'Property':
      ✗ Item Power (swift, encounter) Illusion: attacks do not gain combat advantage against you (ends if you take damage or become helpless/immobile/restrained)
  [Magic Item] +1 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +2 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +3 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +4 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +5 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +6 cloak of elvenkind — field 'Property':
      ✗ Hood up: -2 to others' Perception to see you and +2 item bonus on Stealth to hide
  [Magic Item] +1 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +2 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +3 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +4 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +5 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +6 cloak of the artist — field 'Property':
      ✗ Item Power (swift, daily): until the end of the encounter, you can be under two Stance powers at once
  [Magic Item] +3 cloak of the bat — field 'Property':
      ✗ In dim light or darkness, grip the cloak to fly speed 8
  [Magic Item] +4 cloak of the bat — field 'Property':
      ✗ In dim light or darkness, grip the cloak to fly speed 8
  [Magic Item] +5 cloak of the bat — field 'Property':
      ✗ In dim light or darkness, grip the cloak to fly speed 8
  [Magic Item] +6 cloak of the bat — field 'Property':
      ✗ In dim light or darkness, grip the cloak to fly speed 8
  [Magic Item] +1 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +2 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +3 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +4 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +5 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +6 cloak of the manta ray — field 'Property':
      ✗ Hood up: breathe underwater and gain a swim speed of 12
  [Magic Item] +1 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +2 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +3 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +4 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +5 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +6 cloak of the skillful — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +1 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +2 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +3 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +4 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +5 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +6 medallion of thoughts — field 'Property':
      ✗ gain insight into the target's reasoning, emotional state and a dominant thought
  [Magic Item] +1 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +2 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +3 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +4 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +5 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +6 necklace of adaptation — field 'Property':
      ✗ +2 to defenses against harmful gases and vapors
  [Magic Item] +1 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +2 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +3 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +4 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +5 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +6 adamantine armor — field 'Property':
      ✗ Item Power (counter, daily): a critical hit against you becomes a normal hit instead
  [Magic Item] +1 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +2 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +3 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +4 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +5 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +6 adaptive armor — field 'Property':
      ✗ After you take typed damage, gain resistance to that type = double the enhancement until you take a different type
  [Magic Item] +4 armor of invulnerability — field 'Property':
      ✗ Resistance to untyped damage = double the enhancement
      ✗ Item Power (counter/swift, daily): immune to untyped damage until the end of your next turn
  [Magic Item] +5 armor of invulnerability — field 'Property':
      ✗ Resistance to untyped damage = double the enhancement
      ✗ Item Power (counter/swift, daily): immune to untyped damage until the end of your next turn
  [Magic Item] +6 armor of invulnerability — field 'Property':
      ✗ Resistance to untyped damage = double the enhancement
      ✗ Item Power (counter/swift, daily): immune to untyped damage until the end of your next turn
  [Magic Item] +1 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +2 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +3 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +4 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +5 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +6 armor of resistance — field 'Property':
      ✗ Resistance to one chosen damage type = double the enhancement bonus
  [Magic Item] +1 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +2 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +3 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +4 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +5 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +6 demon armor — field 'Property':
      ✗ The armor's enhancement also applies to your attack and damage rolls (+Xd6 on a crit)
      ✗ Cursed: cannot be removed without remove curse
      ✗ demons gain combat advantage vs you
  [Magic Item] +1 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +2 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +3 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +4 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +5 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +6 dragon scale armor — field 'Property':
      ✗ +2 to defenses vs Fear and vs dragons' Near attacks
      ✗ resistance to one type = double the enhancement
      ✗ detect the nearest same-type dragon once per day
  [Magic Item] +1 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +2 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +3 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +4 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +5 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +6 dwarf worked armor — field 'Property':
      ✗ Reduce unwilling movement you are subject to by up to 2 squares
  [Magic Item] +1 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +2 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +3 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +4 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +5 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +6 emergency armor — field 'Property':
      ✗ While staggered, +1 to Fortitude, Reflex and Will defenses
  [Magic Item] +1 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +2 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +3 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +4 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +5 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +6 glamored armor — field 'Property':
      ✗ Swift action: make the armor appear to be other clothing or armor
  [Magic Item] +1 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +2 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +3 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +4 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +5 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +6 gnome worked armor — field 'Property':
      ✗ This armor also carries a cloak boost (GM's choice)
  [Magic Item] +1 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +2 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +3 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +4 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +5 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +6 indomitable armor — field 'Property':
      ✗ Stunned becomes dazed
      ✗ immobile becomes slowed
      ✗ restrained becomes immobile
  [Magic Item] +1 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +2 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +3 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +4 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +5 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +6 lifegiving armor — field 'Property':
      ✗ When you spend a recovery to heal, you may spend one more to heal your recovery value again
  [Magic Item] +1 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +2 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +3 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +4 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +5 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +6 ophiduan armor — field 'Property':
      ✗ Ignore this armor's armor-check and speed penalties
  [Magic Item] +1 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +2 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +3 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +4 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +5 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +6 skillful armor — field 'Property':
      ✗ Add the enhancement bonus to checks with a chosen skill
  [Magic Item] +1 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
  [Magic Item] +2 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
  [Magic Item] +3 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
  [Magic Item] +4 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
  [Magic Item] +5 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
  [Magic Item] +6 stubborn armor — field 'Property':
      ✗ On a successful save against persistent damage, gain temporary hit points equal to the persistent damage value
```
### equipment/magic-items-consumables.yaml
```
  [Magic Item] Oil of Sharpness +1 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Oil of Sharpness +2 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Oil of Sharpness +3 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Oil of Sharpness +4 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Oil of Sharpness +5 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Oil of Sharpness +6 — field 'Description':
      ✗ Apply to a mundane weapon (standard action): it becomes an enchanted weapon with this bonus until the end of the encounter
  [Magic Item] Tonic of Agility +1 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Agility +2 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Agility +3 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Agility +4 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Agility +5 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Agility +6 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Reflex and Acrobatics
      ✗ stand from prone as a free action (ends effect)
      ✗ reroll an Acrobatics check (ends effect)
  [Magic Item] Tonic of Alertness +1 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Alertness +2 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Alertness +3 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Alertness +4 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Alertness +5 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Alertness +6 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Will and Perception
  [Magic Item] Tonic of Endurance +1 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Endurance +2 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Endurance +3 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Endurance +4 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Endurance +5 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Endurance +6 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to Fortitude and Endure
      ✗ resistance to poison 5 (10 at L11, 15 at L21)
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +1 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +2 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +3 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +4 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +5 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Regeneration +6 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to recovery value and regeneration X
      ✗ spend a recovery to heal (ends effect)
  [Magic Item] Tonic of Strength +1 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Tonic of Strength +2 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Tonic of Strength +3 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Tonic of Strength +4 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Tonic of Strength +5 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Tonic of Strength +6 — field 'Description':
      ✗ Swift action, lasts 5 minutes: +X item bonus to melee damage rolls, Athletics and Strength checks
  [Magic Item] Potion of Heroism (Level 9) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one daily or encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Potion of Heroism (Level 19) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one daily or encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Potion of Heroism (Level 29) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one daily or encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Potion of Vitality (Level 7) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Potion of Vitality (Level 17) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Potion of Vitality (Level 27) — field 'Description':
      ✗ Swift action: spend a recovery and un-expend one encounter power (attack or utility) of the potion's level or lower
  [Magic Item] Oil of Dullness — field 'Description':
      ✗ Standard action: apply to a magic item
      ✗ it becomes mundane (no magic) until thoroughly cleaned
  [Magic Item] Potion of Healing — field 'Description':
      ✗ Swift action: the user spends a recovery and the target heals 10 HP
  [Magic Item] Potion of Healing and Rescue — field 'Description':
      ✗ Swift action: the user spends a recovery
      ✗ the target heals 25 HP and makes a saving throw against a save-ends effect
  [Magic Item] Potion of Healing and Rescue (Advanced) — field 'Description':
      ✗ Swift action: the user spends a recovery
      ✗ the target heals 50 HP and makes a saving throw against a save-ends effect
  [Magic Item] Salve of Resurrection — field 'Description':
      ✗ Standard action: apply to an adjacent creature that died within the last round
      ✗ it is resurrected and healed 50 HP
  [Magic Item] Skeleton Key — field 'Description':
      ✗ Swift action: insert into any lock
      ✗ if it would open on a Sleight of Hand result of 30, it opens and the key turns to dust
```
### equipment/magic-items-wondrous.yaml
```
  [Magic Item] Headband of Intellect — field 'Description':
      ✗ Item Power (free, encounter): when you miss an Intelligence attack or fail an Intelligence skill check, reroll it
  [Magic Item] Helm of Brilliance — field 'Description':
      ✗ Radiant light aura 6 (undead starting their turn in it take 5 radiant)
      ✗ Item Power (free, enc, Fire): a hit deals +2d6 fire
  [Magic Item] Helm of Comprehending Languages — field 'Description':
      ✗ You are under the effect of the understand languages incantation
  [Magic Item] Belt of Dwarvenkind — field 'Description':
      ✗ speak/read/write Dwarvish
  [Magic Item] Belt of Giant Strength — field 'Description':
      ✗ Item Power (free, encounter): when you miss a Strength attack or fail a Strength skill check, reroll it
  [Magic Item] Belt of the Archer (Level 5) — field 'Description':
      ✗ Your basic ranged attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Belt of the Archer (Level 15) — field 'Description':
      ✗ Your basic ranged attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Belt of the Archer (Level 25) — field 'Description':
      ✗ Your basic ranged attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Belt of the Man-at-Arms (Level 5) — field 'Description':
      ✗ Your basic melee attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Belt of the Man-at-Arms (Level 15) — field 'Description':
      ✗ Your basic melee attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Belt of the Man-at-Arms (Level 25) — field 'Description':
      ✗ Your basic melee attacks deal +2 damage (+4 at L15, +6 at L25)
  [Magic Item] Arrow-Catching Shield (Level 6) — field 'Description':
      ✗ +1 AC vs ranged attacks (+2 at L16, +3 at L26)
      ✗ Item Power (counter, enc): become the target of a ranged attack aimed at an adjacent ally
  [Magic Item] Arrow-Catching Shield (Level 16) — field 'Description':
      ✗ +1 AC vs ranged attacks (+2 at L16, +3 at L26)
      ✗ Item Power (counter, enc): become the target of a ranged attack aimed at an adjacent ally
  [Magic Item] Arrow-Catching Shield (Level 26) — field 'Description':
      ✗ +1 AC vs ranged attacks (+2 at L16, +3 at L26)
      ✗ Item Power (counter, enc): become the target of a ranged attack aimed at an adjacent ally
  [Magic Item] Gloves of Swimming and Climbing — field 'Description':
      ✗ Climb and swim at your walk speed without extra cost
      ✗ +2 item bonus to Athletics to climb or swim
  [Magic Item] Ring of Evasion — field 'Description':
      ✗ Item Power (immediate, encounter): when an attack vs Reflex hits you, the attacker rerolls and uses the second result
  [Magic Item] Ring of Invisibility — field 'Description':
      ✗ Item Power (standard, daily, Illusion): turn invisible until the encounter ends, you attack, or you end it (swift)
  [Magic Item] Ring of Mind Shielding — field 'Description':
      ✗ Creatures cannot read your thoughts, detect lies, or know your alignment/ancestry
      ✗ You may make the ring invisible
  [Magic Item] Ring of Regeneration — field 'Description':
      ✗ While you have at least 1 HP, regain 5 HP each round
      ✗ regrow lost body parts over 1d6+1 days
  [Magic Item] Ring of Resistance (Level 6) — field 'Description':
      ✗ Resistance 5 to one chosen damage type (10 at L16, 15 at L26)
  [Magic Item] Ring of Resistance (Level 16) — field 'Description':
      ✗ Resistance 5 to one chosen damage type (10 at L16, 15 at L26)
  [Magic Item] Ring of Resistance (Level 26) — field 'Description':
      ✗ Resistance 5 to one chosen damage type (10 at L16, 15 at L26)
  [Magic Item] Ring of Warmth — field 'Description':
      ✗ Resistance to fire 2
      ✗ unharmed by temperatures as low as -50F
  [Magic Item] Ring of Water Walking — field 'Description':
      ✗ Stand on and move across any liquid surface as if solid
  [Magic Item] Ring of X-ray Vision — field 'Description':
      ✗ Swift action: see through solid matter (radius 6) for 1 minute
  [Magic Item] Boots of Feather Falling — field 'Description':
      ✗ When you fall, you descend 12 squares/round and take no falling damage
  [Magic Item] Boots of Jumping — field 'Description':
      ✗ Item Power (move, encounter): jump up to your speed without provoking opportunity attacks
  [Magic Item] Boots of the Winterlands — field 'Description':
      ✗ tolerate extreme cold (-50F, -100F in heavy clothes)
  [Magic Item] Winged Boots — field 'Description':
      ✗ Flying speed equal to your walking speed, up to 4 hours per day
  [Magic Item] Bracelet of Friends — field 'Description':
      ✗ Grasp a charm and speak a keyed ally's name to summon that willing ally (and their gear) to you across the same plane
      ✗ the charm then vanishes
  [Magic Item] Decanter of Endless Water — field 'Description':
      ✗ Command word pours endless fresh or salt water (stream 1 gal/round, or fountain 5 gal/round)
  [Magic Item] Efficient Quiver — field 'Description':
      ✗ Three extradimensional compartments (60 arrows/bolts, 18 javelins, 6 long items) weighing only 2 lb
      ✗ draw items freely
  [Magic Item] Eyes of Minute Seeing — field 'Description':
      ✗ +2 enhancement bonus to sight-based checks searching or studying within 1 foot
  [Magic Item] Eyes of the Eagle — field 'Description':
      ✗ make out distant details
  [Magic Item] Eversmoking Bottle — field 'Description':
      ✗ Item Power (standard, daily, Fire/Zone): Near burst 6 of thick smoke (heavily obscured) for 5 minutes or until strong wind
  [Magic Item] Folding Boat — field 'Description':
      ✗ Command words unfold the box into a skiff or a ship, or fold it back into a box
  [Magic Item] Goggles of Night — field 'Description':
      ✗ Darkvision 12 (or +12 to existing darkvision)
  [Magic Item] Hand of the Mage — field 'Description':
      ✗ You can use one cantrip (GM's choice from the Cantrips discipline)
  [Magic Item] Horseshoes of a Zephyr — field 'Description':
      ✗ A shod mount can fly at its walk speed (hover, max altitude 1) and travel 12 hours/day without exhaustion
  [Magic Item] Horseshoes of Speed — field 'Description':
      ✗ A shod mount's walking speed increases by 6
  [Magic Item] Immovable Rod — field 'Description':
      ✗ Swift action: fix the rod in place (holds up to 8,000 lb) until pressed again
  [Magic Item] Instant Fortress — field 'Description':
      ✗ Standard action: grow a 4x4x6-square adamantine tower (walls/door/roof: 100 HP, resist 15)
      ✗ dismiss when empty
  [Magic Item] Lantern of Revealing — field 'Description':
      ✗ Sheds bright light (6) / dim (6 more)
      ✗ reveals invisible creatures/objects in its bright light
  [Magic Item] Marvelous Pigments — field 'Description':
      ✗ Paint inanimate objects or terrain (up to 2 squares high) into reality
      ✗ nothing worth more than 25 gp
  [Magic Item] Necrosis Cube — field 'Description':
      ✗ Regenesis: spending a recovery heals +Wis mod (Wis+2 at L11, Wis+5 at L21)
      ✗ No need to eat/drink
      ✗ 2h long rest
      ✗ Healing Light (reaction, enc): when a creature within 6 uses an arcane power, spend a recovery to heal
  [Magic Item] Pipes of the Sewers — field 'Description':
      ✗ they will not attack you unless threatened
  [Magic Item] Portable Hole — field 'Description':
      ✗ Unfolds into a 2-square-deep extradimensional hole
      ✗ fold it up to carry its contents (10 minutes of air)
  [Magic Item] Robe of Eyes — field 'Description':
      ✗ see invisible/Ethereal out to 24
      ✗ Radiant hits daze you (save ends)
  [Magic Item] Rope of Climbing — field 'Description':
      ✗ A 60-ft rope (holds 3,000 lb) that moves on command and can knot/unknot itself
  [Magic Item] Satchel of Useful Items — field 'Description':
      ✗ Detach cloth patches (swift action) that become real items (dagger, lantern, rope, pit, mule, etc
  [Magic Item] Script of Faithfulness — field 'Description':
      ✗ The holder is warned of actions or items that would harm their alignment or standing with their deity
  [Magic Item] Spiritlink Charm — field 'Description':
      ✗ On a critical hit, your companion deals +1d6 per point of your enchanted weapon/focus bonus
  [Magic Item] Stone of Alarm — field 'Description':
      ✗ Affix to an object
      ✗ if touched without the command word, it screeches for 1 hour (audible up to a quarter-mile)
  [Magic Item] Sustaining Spoon — field 'Description':
      ✗ Placed in an empty container, fills it with nourishing gruel enough to feed up to four humans per day
  [Magic Item] Bag of Tricks — field 'Description':
      ✗ Pull Out a Mook (standard, daily, Summons): summon the bag's associated mook as a companion (level = the mook's)
      ✗ It vanishes at encounter end or 0 HP
```
### equipment/magic-items.yaml
```
  [Magic Item] Enchanted Weapon +1 — field 'Description':
      ✗ +1 enhancement bonus to attack and damage rolls with that weapon
      ✗ +1d6 damage on a critical hit
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +1 — field 'Description':
      ✗ +1 enhancement bonus to attack and damage rolls with that focus
      ✗ +1d6 damage on a critical hit
  [Magic Item] Enchanted Armor +1 — field 'Description':
      ✗ +1 enhancement bonus to Armor Class
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +1 — field 'Description':
      ✗ Includes amulets, necklaces and talismans
  [Magic Item] Enchanted Weapon +2 — field 'Description':
      ✗ +2 enhancement bonus to attack and damage rolls with that weapon
      ✗ +2d6 damage on a critical hit
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +2 — field 'Description':
      ✗ +2 enhancement bonus to attack and damage rolls with that focus
      ✗ +2d6 damage on a critical hit
  [Magic Item] Enchanted Armor +2 — field 'Description':
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +2 — field 'Description':
      ✗ +2 enhancement bonus to Fortitude, Reflex and Will defenses
      ✗ Includes amulets, necklaces and talismans
  [Magic Item] Enchanted Weapon +3 — field 'Description':
      ✗ +3 enhancement bonus to attack and damage rolls with that weapon
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +3 — field 'Description':
      ✗ +3 enhancement bonus to attack and damage rolls with that focus
  [Magic Item] Enchanted Armor +3 — field 'Description':
      ✗ +3 enhancement bonus to Armor Class
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +3 — field 'Description':
      ✗ +3 enhancement bonus to Fortitude, Reflex and Will defenses
      ✗ Includes amulets, necklaces and talismans
  [Magic Item] Enchanted Weapon +4 — field 'Description':
      ✗ +4 enhancement bonus to attack and damage rolls with that weapon
      ✗ +4d6 damage on a critical hit
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +4 — field 'Description':
      ✗ +4 enhancement bonus to attack and damage rolls with that focus
      ✗ +4d6 damage on a critical hit
  [Magic Item] Enchanted Armor +4 — field 'Description':
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +4 — field 'Description':
      ✗ +4 enhancement bonus to Fortitude, Reflex and Will defenses
      ✗ Includes amulets, necklaces and talismans
  [Magic Item] Enchanted Weapon +5 — field 'Description':
      ✗ +5 enhancement bonus to attack and damage rolls with that weapon
      ✗ +5d6 damage on a critical hit
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +5 — field 'Description':
      ✗ +5 enhancement bonus to attack and damage rolls with that focus
      ✗ +5d6 damage on a critical hit
  [Magic Item] Enchanted Armor +5 — field 'Description':
      ✗ +5 enhancement bonus to Armor Class
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +5 — field 'Description':
      ✗ +5 enhancement bonus to Fortitude, Reflex and Will defenses
      ✗ Includes amulets, necklaces and talismans
  [Magic Item] Enchanted Weapon +6 — field 'Description':
      ✗ +6 enhancement bonus to attack and damage rolls with that weapon
      ✗ +6d6 damage on a critical hit
      ✗ (Bindings enchant unarmed/natural attacks the same way
  [Magic Item] Enchanted Focus +6 — field 'Description':
      ✗ +6 enhancement bonus to attack and damage rolls with that focus
      ✗ +6d6 damage on a critical hit
  [Magic Item] Enchanted Armor +6 — field 'Description':
      ✗ +6 enhancement bonus to Armor Class
      ✗ Light armor gives +1 more in the prestige tier and +1 again in the epic tier
      ✗ heavy armor gives double this bonus
  [Magic Item] Enchanted Cloak +6 — field 'Description':
      ✗ +6 enhancement bonus to Fortitude, Reflex and Will defenses
      ✗ Includes amulets, necklaces and talismans
```
### feats.yaml
```
  [Feat] Energy Resistance — field 'Benefit':
      ✗ Choose acid, cold, fire, force, lightning, necrotic, poison, psychic, radiant or thunder
      ✗ You gain resistance to that damage type equal to your level
  [Feat] Melee Finesse — field 'Benefit':
      ✗ Use that ability modifier for your basic melee attack rolls instead of Strength, and add half that modifier to damage in lieu of your Strength modifier if it is higher
```
### kits.yaml
```
  [Theme] Embodies Strength — field 'Description':
      ✗ You are a beast - an imposing behemoth or a gentle giant who raises a hand only when necessary
  [Class Feature] Full Torque — field 'Description':
      ✗ Use Strength in place of Dexterity for attack and damage rolls with thrown (light) weapons and with sling and bow weapons
      ✗ Use Strength in place of Charisma for Intimidate checks
  [Class Feature] No Time for Pain — field 'Description':
      ✗ While staggered, add your Strength modifier to your recovery value
  [Class Feature] Comical Reaction — field 'Description':
      ✗ When you succeed on a saving throw against blinded, dazed, deafened, slowed, stunned or weakened, you gain a basic attack as a free action
  [Theme] Embodies Speed — field 'Description':
      ✗ You have perfected a body designed for raw speed and agility, keeping an entire landscape of paths and escape routes in mind
  [Class Feature] Power To Weight Ratio — field 'Description':
      ✗ Use Dexterity in place of Strength for Athletics climb/jump checks and for attack and damage with unarmed attacks, one-handed melee weapons and grapples
  [Class Feature] Speed Vault — field 'Description':
      ✗ If you move at least 2 squares running toward a wall, you gain a climb speed equal to your remaining movement
  [Class Feature] Split-Slide — field 'Description':
      ✗ Once per round as a swift action, choose one enemy in line of sight
      ✗ you do not provoke opportunity attacks from it and can move through its square (not ending there)
  [Class Feature] Channel Divinity - Shielded Soul — field 'Description':
      ✗ If you cannot already Channel Divinity, you gain it once per encounter (any Channel Divinity power you know)
  [Class Feature] Brothers in Arms — field 'Description':
      ✗ When you are a target of a Near or Far attack, you and all allied targets gain a bonus to defense against it equal to the number of allied targets
  [Class Feature] Channel Divinity - Light Ward — field 'Description':
      ✗ If you cannot already Channel Divinity, you gain it once per encounter (any Channel Divinity power you know)
  [Class Feature] Channel Divinity - Guidance — field 'Description':
      ✗ If you cannot already Channel Divinity, you gain it once per encounter (any Channel Divinity power you know)
  [Class Feature] Disciple of Life — field 'Description':
      ✗ Add your Wisdom modifier to the amount you heal with powers that have the Healing tag
  [Class Feature] Channel Divinity - Spur On — field 'Description':
      ✗ If you cannot already Channel Divinity, you gain it once per encounter (any Channel Divinity power you know)
  [Class Feature] Disciple of Tyranny — field 'Description':
      ✗ You are trained in Intimidate (if not already)
```
### paths/epic.yaml
```
  [Class Feature] Appropriation — field 'Description':
      ✗ it appears under your bedroll, ready for use, and disappears when you begin your next long rest
      ✗ The item is summoned, not created
  [Class Feature] Interplanar Contingencies — field 'Description':
      ✗ Once per day, when you die, you appear the following round with half your maximum HP and slowed, in an unoccupied space at least 6 squares from your body, with copies of your equipment
      ✗ Merge the bodies with a swift action while adjacent to your corpse
  [Class Feature] The Economic Flow — field 'Description':
      ✗ If something is for sale anywhere in the planes, you can purchase it as a free action
      ✗ the object appears and the payment (which must be on your person) disappears
  [Power] Force Sphere — field 'Effect':
      ✗ Create an impenetrable sphere of force in a near burst between 1 and 5 squares in radius (your choice)
  [Class Feature] Against the Laws of Physics — field 'Description':
      ✗ Pick one of your daily utility powers
  [Class Feature] Beyond Impossible — field 'Description':
      ✗ instead, gain a +20 power bonus to your next skill roll with the skill you selected with Out of Anyone's League
  [Epic Path] Most Dangerous — field 'Description':
      ✗ You're a ghost, one of the most wanted individuals on the planet
  [Class Feature] Second Nature — field 'Description':
      ✗ You reroll natural 1s and 2s on attack rolls and skill checks, but must take the second result even if it is another 1 or a 2
  [Class Feature] Implausible Speed — field 'Description':
      ✗ You can use two action points per encounter, and gain two action points when you reach a streak
  [Epic Path] Respected — field 'Description':
      ✗ Every soldier knows your face
  [Class Feature] Sacrifice Play — field 'Description':
      ✗ As an immediate reaction once per encounter, when an ally is reduced below 0 hit points, you can grant any other ally in line of sight a standard, a move and a swift action
  [Class Feature] Master Tactician — field 'Description':
      ✗ Once per encounter, as a standard action, give one swift, one move and one standard action to be split among up to three allies of your choice in line of sight
  [Class Feature] Team Support — field 'Description':
      ✗ that ally immediately saves against one effect a save can end (except dying)
  [Class Feature] Where the Need is Greatest — field 'Description':
      ✗ As a swift action, choose an ally (or yourself) to lose one recovery
      ✗ then choose an ally (or yourself) to gain one recovery
  [Class Feature] Why Won't You Die? — field 'Description':
      ✗ Attacks cannot score critical hits against you (you take normal damage instead)
  [Class Feature] Risky Maneuver — field 'Description':
      ✗ As a swift action, reduce all your defense values to 1 until the start of your next turn
  [Power] Lasting Image — field 'Effect':
      ✗ You can take a single action on your turn (plus free/immediate actions and opportunity attacks), gaining a +2 bonus to attack rolls and +5 to damage rolls until the end of the encounter
      ✗ You cannot be healed and do not make death saves until then, when you fall unconscious and begin making death saves as normal
```
### paths/prestige.yaml
```
  [Prestige Path] Assassin — field 'Description':
      ✗ You move in quickly and quietly, dispatch the target, and vanish into the shadows
  [Power] The Professional — field 'Effect':
      ✗ Until the end of the encounter, when you are granted a basic attack outside of your turn, you do additional damage equal to your Dexterity modifier +2 if you hit
      ✗ Level 21: additional damage equal to your Dexterity modifier +5
  [Prestige Path] Battlefield Healer — field 'Description':
      ✗ You specialise in impromptu medical care, in the field, under the pressures of combat - still armed, protecting your team with blades and bandages
  [Power] Race to the Fallen — field 'Effect':
      ✗ The target ally can spend a recovery and regain its recovery value plus 3d6 additional hit points
  [Power] No Longer Civilized — field 'Miss':
      ✗ Repeat the attack against the same target at -2 to the attack roll but +1dW damage on a hit
  [Prestige Path] Bounty Hunter — field 'Description':
      ✗ You analyse the actions and motivations of individuals, pinpoint specific threats, and coordinate others to remove them - with weapons as a backup
```

## Coverage — transcribed vs source, by discipline

| Discipline | source | transcribed | missing (omitted) |
|---|--:|--:|---|
| Art of War | 18 | 18 | — |
| Blades in the Dark | 23 | 23 | — |
| Cup of Brimstone | 25 | 25 | — |
| Elemental Flux | 31 | 31 | — |
| Frontline Fighting | 37 | 37 | — |
| Golden Lion | 24 | 24 | — |
| Juggernautical | 21 | 21 | — |
| Last Laugh | 35 | 35 | — |
| Red in Tooth and Claw | 71 | 71 | — |
| Seershot | 30 | 30 | — |
| Spells of Ice and Fire | 18 | 18 | — |
| Starfall | 42 | 42 | — |
| Strong Bidding | 14 | 14 | — |
| Veiled Moon | 32 | 32 | — |

## YAML powers whose name is in NO source book (check for rename/fabrication)

- ancestries-species.yaml :: Vengeance of the Pit

