# Échantillon d'échecs — 20260728-v5-google-gemma-4-12b-qat-d79a63b9

> seed **20260808** — 20 item(s) tirés parmi les directions dont
> R̄ < 0,750. Renseigner `étiquette humaine :` pour
> **chaque** item, puis relancer :
>
> `analyze --review eval/analysis/20260728-v5-google-gemma-4-12b-qat-d79a63b9.sample.md`
>
> Vocabulaire fermé — `M0` réussi · `M1` trop générique · `M2` n'attrape qu'une face · `M3` collision avec un distracteur · `M4` relation trop indirecte · `M5` jargon / mot rare (manuel) · `M6` collision inter-directions (board) · `M?` non classé
>
> `M5` ne sort **jamais** automatiquement : c'est ici, et seulement ici, qu'il se pose.

## 1 — dev-001 / Bottom

- indice : Perle
- paire cible : Liquide + Collier
- R̄ : 0,500
- 16 mots : Paradis, Membre, Vêtement, Chêne, Terrasse, Tarte, Déchet, Voleur, Maître, Herbe, Liquide, Fable, Miroir, Fuite, Collier, Ampoule
- décodage 0 : Collier + Ampoule   (R = 0,500)
- décodage 1 : Collier + Fable   (R = 0,500)
- décodage 2 : Collier + Paradis   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 2 — dev-001 / Right

- indice : Pâtisserie
- paire cible : Tarte + Herbe
- R̄ : 0,667
- 16 mots : Paradis, Membre, Vêtement, Chêne, Terrasse, Tarte, Déchet, Voleur, Maître, Herbe, Liquide, Fable, Miroir, Fuite, Collier, Ampoule
- décodage 0 : Tarte + Herbe   (R = 1,000)
- décodage 1 : Tarte + Membre   (R = 0,500)
- décodage 2 : Tarte + Fable   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 3 — dev-016 / Top

- indice : Sauvetage
- paire cible : Radeau + Incendie
- R̄ : 0,500
- 16 mots : Radeau, Bouillotte, Menthe, Tuile, Incendie, Sucre, Robot, Sapin, Rideau, Botte, Pied, Grenade, Handicap, Couture, Pâte, Maître
- décodage 0 : Radeau + Sucre   (R = 0,500)
- décodage 1 : Radeau + Bouillotte   (R = 0,500)
- décodage 2 : Grenade + Radeau   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M0 (c'est un bon indice: on sauve des gens sur un radeau, tout comme on sauve des gens d'un incendie)

## 4 — dev-003 / Left

- indice : Porte
- paire cible : Mare + Clé
- R̄ : 0,500
- 16 mots : Huile, Couvert, Lent, Clé, Bourse, Géant, Déchet, Montagne, Ombre, Cougar, Coussin, Paresseux, Rayon, Dieu, Assassin, Mare
- décodage 0 : Clé + Bourse   (R = 0,500)
- décodage 1 : Clé + Couvert   (R = 0,500)
- décodage 2 : Couvert + Clé   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 5 — dev-036 / Right

- indice : Sanitaire
- paire cible : Métal + Toilettes
- R̄ : 0,500
- 16 mots : Petit, Jungle, Seau, Tampon, Mythe, Métal, Rasoir, Pendule, Reptile, Toilettes, Boudin, Juge, Carton, Ficelle, France, Montre
- décodage 0 : Toilettes + Tampon   (R = 0,500)
- décodage 1 : Toilettes + Tampon   (R = 0,500)
- décodage 2 : Toilettes + Tampon   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 6 — dev-026 / Left

- indice : Naval
- paire cible : Bleu + Ordre
- R̄ : 0,000
- 16 mots : Pile, Caverne, Météo, Ordre, Cabine, Ange, Courrier, Lunettes, Riz, Roi, Chine, Héroïne, Dessert, Réparation, Urgence, Bleu
- décodage 0 : Caverne + Roi   (R = 0,000)
- décodage 1 : Courrier + Caverne   (R = 0,000)
- décodage 2 : Caverne + Roi   (R = 0,000)
- étiquette auto : M3  (collision avec un distracteur)
- étiquette humaine : M4 (un indice comme "Police" aurait été beaucoup plus pertinent, la police dispose d'un uniforme bleu et représente l'ordre)

## 7 — dev-028 / Right

- indice : Chat
- paire cible : Souris + Liquide
- R̄ : 0,500
- 16 mots : Louche, Paresseux, Nuage, Banque, Lent, Souris, Laser, Nourriture, Oiseau, Liquide, Petit, Ampoule, Europe, Salade, Feutre, Méduse
- décodage 0 : Souris + Nourriture   (R = 0,500)
- décodage 1 : Souris + Louche   (R = 0,500)
- décodage 2 : Souris + Nourriture   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M3 (un chat peut être considéré paresseux, tout comme il chasse des oiseaux, et mange de la nourriture)

## 8 — dev-002 / Top

- indice : Bande
- paire cible : Marron + Collier
- R̄ : 0,500
- 16 mots : Marron, Épée, Poudre, Dieu, Collier, Lac, Grenier, Briquet, Bleu, Automne, Chevalier, Doux, Tête, Club, Vol, Boucle
- décodage 0 : Collier + Boucle   (R = 0,500)
- décodage 1 : Club + Collier   (R = 0,500)
- décodage 2 : Collier + Boucle   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M4 (Bande est un terme très large pour évoquer un collier, et il n'évoque aucune couleur)

## 9 — dev-040 / Bottom

- indice : Plume
- paire cible : Sable + Aile
- R̄ : 0,500
- 16 mots : Collier, Canapé, Histoire, Pari, Orage, Melon, Rame, Membre, Acteur, Bateau, Sable, Blanc, Collant, Pompier, Aile, Religion
- décodage 0 : Aile + Collier   (R = 0,500)
- décodage 1 : Aile + Orage   (R = 0,500)
- décodage 2 : Acteur + Aile   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 10 — dev-027 / Top

- indice : Jeu
- paire cible : Carte + Bague
- R̄ : 0,500
- 16 mots : Carte, Croissant, Radar, Couvert, Bague, Tapis, Cafard, Laine, Boisson, Miel, Trône, Quartier, Automne, Faille, Biberon, Trésor
- décodage 0 : Carte + Trésor   (R = 0,500)
- décodage 1 : Carte + Trésor   (R = 0,500)
- décodage 2 : Carte + Trésor   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (Trésor aurait éte parfait en tant qu'indice, mais malheuresement pour le codeur, il faisait déjà parti des mots à éviter)

## 11 — dev-006 / Bottom

- indice : Courant
- paire cible : Canal + Tornade
- R̄ : 0,333
- 16 mots : Insecte, Odorat, Corde, Savon, Vampire, Pile, Suisse, Perroquet, Papier, Tradition, Canal, Cocktail, Peluche, Ordre, Tornade, Sport
- décodage 0 : Pile + Canal   (R = 0,500)
- décodage 1 : Corde + Canal   (R = 0,500)
- décodage 2 : Pile + Sport   (R = 0,000)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M0 (Un canal fait penser à un courant d'eau, et une tornade est bien un courant d'air)

## 12 — dev-011 / Right

- indice : Verre
- paire cible : Cimetière + Bière
- R̄ : 0,167
- 16 mots : Porte, Plastique, Radeau, Attraction, Poésie, Cimetière, Suite, Paysage, Arme, Bière, Oreille, Roue, Manche, Ferme, Témoin, Billet
- décodage 0 : Porte + Plastique   (R = 0,000)
- décodage 1 : Bière + Plastique   (R = 0,500)
- décodage 2 : Billet + Porte   (R = 0,000)
- étiquette auto : M3  (collision avec un distracteur)
- étiquette humaine : M2 (Verre fonctionne très bien avec Bière, mais absolument pas avec Cimetière)

## 13 — dev-006 / Right

- indice : Accumulation
- paire cible : Pile + Tradition
- R̄ : 0,500
- 16 mots : Insecte, Odorat, Corde, Savon, Vampire, Pile, Suisse, Perroquet, Papier, Tradition, Canal, Cocktail, Peluche, Ordre, Tornade, Sport
- décodage 0 : Papier + Tradition   (R = 0,500)
- décodage 1 : Papier + Pile   (R = 0,500)
- décodage 2 : Papier + Pile   (R = 0,500)
- étiquette auto : M3  (collision avec un distracteur)
- étiquette humaine : M2 (une pile accumule de l'énergie, mais le rapport avec tradition est vraiment éloigné. Note: Batterie aurait peut-être été un bon indice: une batterie accumule de l'énerge telle une pile, et une batterie représente aussi un instrument de musique qui peut évoquer des percussions musicales inhérentes à certaines traditions)

## 14 — dev-019 / Left

- indice : Creuser
- paire cible : Puits + Plateau
- R̄ : 0,333
- 16 mots : Peluche, Cavalier, Histoire, Plateau, Pelle, Hache, Paresseux, Soeur, Toilettes, Fou, Contrôle, Couette, Lave, Carré, Voleur, Puits
- décodage 0 : Hache + Pelle   (R = 0,000)
- décodage 1 : Puits + Pelle   (R = 0,500)
- décodage 2 : Puits + Pelle   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (Note: Volcan aurait été un bon indice, si seulement "Lave" ne faisait pas partie des mots interdits)

## 15 — dev-021 / Right

- indice : Course
- paire cible : Rapide + Régime
- R̄ : 0,500
- 16 mots : Coeur, Infirmier, Studio, Bois, Fin, Rapide, Salade, Copain, Feutre, Régime, Belgique, Patin, Gris, Tomate, Pince, Éclair
- décodage 0 : Patin + Rapide   (R = 0,500)
- décodage 1 : Patin + Rapide   (R = 0,500)
- décodage 2 : Patin + Rapide   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (l'indice "Sport" aurait probablement été meilleur ici: un sportif court vite, et le sport accompagne souvent les individus faisant un régime alimentaire)

## 16 — dev-033 / Right

- indice : Église
- paire cible : Tabac + Cloche
- R̄ : 0,500
- 16 mots : Radar, Sang, Tonnerre, Lame, Bras, Tabac, Premier, Carrefour, Robe, Cloche, Navet, Mouton, Déchet, Fer, Enceinte, Serviette
- décodage 0 : Cloche + Fer   (R = 0,500)
- décodage 1 : Cloche + Robe   (R = 0,500)
- décodage 2 : Carrefour + Cloche   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2

## 17 — dev-039 / Right

- indice : Rouge
- paire cible : Sang + Désert
- R̄ : 0,500
- 16 mots : Gant, Poussin, Feutre, Rire, Gris, Sang, Double, Pétard, Botte, Désert, Vêtement, Oeuf, Épice, Lame, Réparation, Bain
- décodage 0 : Sang + Botte   (R = 0,500)
- décodage 1 : Pétard + Sang   (R = 0,500)
- décodage 2 : Sang + Pétard   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (Un indice comme "Chaud" aurait été meilleur: on pense à l'expression "avoir le sang chaud", et le désert est un endroit chaud)

## 18 — dev-005 / Left

- indice : Musée
- paire cible : Tableau + Froid
- R̄ : 0,500
- 16 mots : Belgique, Hiver, Herbe, Froid, Monnaie, Ciel, Tapis, Manuel, Tempête, Asie, Fumée, Boulanger, Bouche, Coton, Poudre, Tableau
- décodage 0 : Tableau + Manuel   (R = 0,500)
- décodage 1 : Tableau + Manuel   (R = 0,500)
- décodage 2 : Tableau + Manuel   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (Ici en tant qu'humain j'aurais tenté l'indice "Morte": référence à la nature morte pour tableau, et un cadavre froid)

## 19 — dev-023 / Right

- indice : Noël
- paire cible : Fête + Sapin
- R̄ : 0,500
- 16 mots : Dur, Alliance, Forme, Gant, Corbeau, Fête, Marin, Micro, Cage, Sapin, Hôpital, Dictateur, Corbeille, Bouquet, Barrière, Fusil
- décodage 0 : Bouquet + Sapin   (R = 0,500)
- décodage 1 : Sapin + Bouquet   (R = 0,500)
- décodage 2 : Sapin + Bouquet   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M0 (Je suis très surpris de l'étiquette auto car l'indice "Noël" est absolument parfait ici)

## 20 — dev-022 / Bottom

- indice : Matière
- paire cible : Bois + Bleu
- R̄ : 0,500
- 16 mots : Prise, Lapin, Doigt, Vision, Carotte, Paresseux, Assassin, Patin, Pinceau, Fontaine, Bois, Fil, Cochon, Corps, Bleu, École
- décodage 0 : Bois + Pinceau   (R = 0,500)
- décodage 1 : Bois + Fil   (R = 0,500)
- décodage 2 : Bois + Fil   (R = 0,500)
- étiquette auto : M2  (n'attrape qu'une face)
- étiquette humaine : M2 (Ici en tant qu'humain j'aurais peut-être essayé l'indice "Ile": une ile est souvent boisée, et entouré d'un océan bleu)

