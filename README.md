# Sir Cel Shading

Greffon côté joueur pour Space Engineers, chargé par Pulsar. Il ne fait qu'une
chose : du **cel shading façon film d'animation**, dans l'esprit de Breath of
the Wild ou des films du studio Ghibli.

- Des ombres en deux ou trois tons doux, jamais noires.
- Des contours fins, d'une teinte plus sombre que la couleur propre de chaque
  objet.
- Un liseré de lumière sur les silhouettes vues à contre-jour du soleil.
- Une perspective aérienne : plus une chose est loin, plus elle se fond dans la
  couleur du ciel.

Aussi :

- Le joueur active et coupe l'effet quand il veut, sans relancer le jeu.
- Coupé, le jeu dessine avec ses propres shaders : l'image est exactement celle
  du jeu.
- Tout se passe sur la machine du joueur, et un joueur sans le greffon voit le
  rendu du jeu. La seule chose qui part un jour vers un serveur, ce sont les
  rapports d'erreurs et de plantages du greffon, et seulement sur les serveurs
  du studio (PU et PTU) : voir [Rapports d'erreurs](#rapports-derreurs).
- L'interface (HUD, menus, textes) reste nette : elle est dessinée après
  l'effet.

## Utilisation

- **Réglages** : Pulsar, Sir Cel Shading, bouton de réglages. Le premier
  réglage est la case **Activer le greffon**. En dessous : tons d'ombre (2 ou
  3), force des contours, lumière de contour, brume de distance. **Valeurs par
  défaut** les remet à zéro. Chaque changement se voit à l'image suivante.
- **Tchat** : `/cel` active ou coupe l'effet ; `/cel activer`, `/cel couper` ;
  `/cel etat` dit si l'effet est activé ; `/cel aide` rappelle les options. Une
  option inconnue reçoit un message d'erreur qui les liste. Les anciennes
  graphies `on`, `off`, `status` et `help` restent acceptées. La commande n'est
  pas envoyée aux autres joueurs.
- **Réglages enregistrés** dans `%AppData%\SpaceEngineers\Storage\sir-cel-shading\settings.xml`,
  gardés d'une partie à l'autre.
- **Versions antérieures** : le greffon n'a qu'un seul rendu. Un fichier de
  réglages écrit par une version antérieure est lu sans erreur : l'interrupteur
  et les réglages du rendu qui existent encore sont gardés, tout le reste est
  ignoré, et le joueur a le film d'animation. Le fichier de la première version
  (`reglages.xml`) est lu une fois, quand `settings.xml` n'existe pas encore :
  seul son interrupteur est gardé.

## Comment ça marche

Le jeu livre ses effets d'image en source (`Content/Shaders`) et les compile
lui-même au chargement. Sir Cel Shading s'accroche à l'étape des couleurs
finales, `MyToneMapping.Run`, qui est un compute shader
(`Postprocess/Tonemapping/Main.hlsl`) en trois variantes : `m_cs`,
`m_csAlphaLuminance`, `m_csSkip`.

1. Au démarrage, le greffon écrit sa variante de ce shader,
   `Storage\sir-cel-shading\Shaders\CelShading.hlsl`. C'est le corps du jeu
   gardé tel quel (grain, exposition, halo, courbe filmique, filtres), suivi de
   l'effet, juste avant la conversion sRGB. Il inclut les en-têtes du jeu entre
   chevrons, donc ceux du dossier de shaders du jeu.
2. Activé, un préfixe Harmony compile les trois variantes avec le compilateur
   du jeu (`MyShaderCompiler.Compile`, qui refuse sans planter, puis
   `MyComputeShaders.Create`), une fois pour chaque jeu de réglages. Le préfixe
   met ensuite la variante en cours dans le champ statique du jeu, lie la
   profondeur de la scène (`MyGBuffer.Main.ResolvedDepthStencil.SrvDepth`) en
   `t31` et l'albédo de la scène (`MyGBuffer.Main.GBuffer0`) en `t27`. Le
   postfixe rend au champ le shader du jeu et délie les deux emplacements. Un
   finaliseur fait pareil si la passe échoue.
3. Coupé, le préfixe ne touche à rien.

**Contours.** Ils viennent de la dérivée seconde de l'inverse de la distance :
elle est nulle sur une surface plane, et s'allume aux silhouettes et aux
arêtes. La mesure est divisée par la plus petite distance du voisinage et par
l'angle d'un pixel. Elle travaille donc en rapports de distances, jamais en
mètres : une arête est dessinée pareil à un mètre ou à dix kilomètres. Le ciel
se reconnaît à la profondeur vide du jeu. La détection de contours du jeu
(`Postprocess/EdgeDetection.hlsl`) n'est pas utilisée : elle ne marque que la
couverture de l'anticrénelage multi-échantillons, que le jeu n'active plus. Les
contours font un pixel de large, dans une teinte plus sombre et plus profonde
de la couleur propre de l'objet.

**Lumière et couleur.** La couleur finale divisée par l'albédo de l'objet donne
la lumière qu'il reçoit. Le greffon ramène cette lumière sur deux ou trois tons
doux, le plus sombre jamais noir. La lumière au-dessus de la couleur propre de
l'objet (reflets, lampes, flammes) est laissée telle quelle. Là où l'albédo ne
se lit pas (le ciel, un objet presque noir, le multi-échantillonnage),
l'éclairage du jeu est gardé.

**Lumière de contour.** Un pixel à quelques pixels de quelque chose au moins un
tiers plus loin, ou du ciel, est sur une silhouette. Il reçoit un fin liseré de
lumière du soleil, de la couleur du soleil, d'autant plus que la caméra fait
face au soleil.

**Brume de distance.** La brume croît avec la distance mesurée en octaves de la
distance du plan proche, un rapport : elle commence vers cinquante mètres et
est à son maximum vers vingt-cinq kilomètres avec le plan proche habituel du
jeu, et elle ne cache jamais complètement une chose. La couleur du ciel vient de
l'image elle-même : chaque groupe de 8x8 pixels lit un point d'une grille fixe
de 8x8 sur l'écran, et chaque pixel mélange les points de ciel proches de lui,
ceux du dessus pesant davantage. Sous un ciel noir (l'espace), il n'y a pas de
brume.

**Emplacements t31 et t27.** Aucun fichier de `Content/Shaders` ne déclare
`t31` ni `t27`. La passe du jeu n'utilise que `t0` à `t3`, `u0` et `s0` à `s3`,
et le moteur gère 32 emplacements par étape.

**Coût.** Quinze lectures de profondeur, une lecture d'albédo, une lecture de la
couleur de la scène pour la grille de ciel et, pour les pixels lointains
seulement, un mélange de 64 valeurs partagées. Le tout dans une passe que le jeu
exécute de toute façon : aucune passe plein écran n'est ajoutée.

## Arrêts et cohabitation

Chaque nom interne du moteur est résolu par réflexion au démarrage. Plusieurs
cas arrêtent l'effet pour toute la session :

- un nom absent ;
- un en-tête du jeu absent ;
- une variante refusée par le compilateur du jeu ;
- une faute sur le fil de rendu.

Tous passent par le même chemin (`SessionStop`). Le jeu garde son rendu, une
ligne `[sir-cel-shading]` part au journal du jeu, et le joueur reçoit une
notification dès qu'une partie est ouverte. `/cel etat` en donne alors la
raison.

Deux greffons ne se disputent jamais une même étape. Avant de s'accrocher, puis
toutes les dix secondes, Sir Cel Shading regarde qui est accroché à
`MyToneMapping.Run` (`Harmony.GetPatchInfo`). S'il trouve un autre
propriétaire, il cède la place et le dit au joueur.

## Rapports d'erreurs

Le greffon informe le studio de ses propres erreurs et des plantages du jeu,
sous la forme commune à tous les greffons côté joueur du studio. C'est le seul
échange du greffon avec un serveur.

**Ce qui donne un rapport.**

- Chaque exception que le greffon rattrape et chaque ligne d'erreur qu'il écrit
  au journal du jeu (`[sir-cel-shading] ERREUR : ...`) donne un rapport
  `erreur`. Cela comprend chaque arrêt listé plus haut : un shader refusé par le
  compilateur du jeu est au journal du jeu, montré au joueur, et rapporté.
- La même erreur (même message, même haut de pile) est rapportée une fois par
  session de jeu, avec le nombre de fois où elle s'est produite et les dates de
  la première et de la dernière. Elle part 30 secondes après sa première
  apparition, pour que ses répétitions soient comptées.
- Un plantage. Une marque, `Storage\sir-cel-shading\session.marker`, est posée
  au démarrage du jeu et effacée à sa sortie propre. Si le démarrage suivant la
  trouve, la session précédente ne s'est jamais terminée (plantage, ou jeu
  tué). Le greffon relit alors le journal du jeu de cette session (le
  `SpaceEngineers_*.log` précédent, dans `%AppData%\SpaceEngineers`) et
  construit un rapport `plantage` : le bloc CRASH INFO du jeu, l'exception qui a
  fait tomber le jeu et sa pile, les 20 dernières lignes d'erreur du jeu et les
  20 dernières lignes du greffon.

**Le rapport.** Un objet JSON en UTF-8 :
`{ greffon, version, genre, message, pile, premiere, derniere, nombre, session, lignes }`.
`greffon` vaut « Sir Cel Shading », `version` la version du greffon, `genre`
`erreur` ou `plantage`, `pile` la pile d'appels, `premiere` et `derniere` la
première et la dernière fois où l'erreur a été vue et `session` le début de la
session de jeu concernée (le tout en UTC, ISO 8601), `nombre` le compte,
`lignes` des lignes du journal. Au-delà de 32 Ko, les lignes les plus anciennes
sont abandonnées.

**Rien sur le joueur.** Une ligne qui porte un identifiant Steam, un pseudo, un
nom de grille ou une adresse de serveur est retirée en entier : les lignes sont
jugées sur les mots mêmes du jeu (`UserId`, `UserName`, `Connecting to`,
`Grid name`...), sur la forme des données (identifiants Steam et autres longs
nombres, adresses IP, liens `steam://`) et sur les noms connus de la machine (le
pseudo et l'identifiant Steam du joueur, le nom d'utilisateur Windows). Le
dossier de l'utilisateur Windows devient `%USERPROFILE%`. Les lignes d'une pile
d'appels gardent leurs noms de code.

**Seulement sur nos serveurs.** Les rapports partent par
`MyAPIGateway.Multiplayer.SendMessageToServer`, sur le canal de rapports du
studio 5465479001, seulement tant que le joueur est connecté en client à un
serveur dédié dont le nom (ou le nom du monde) contient « Sirius », comme le
font PU et PTU. Les canaux réseau du jeu sont des nombres sur 16 bits :
5465479001 n'y tient pas, on envoie donc ses 16 bits de poids faible, 38745, que
le serveur écoute. Ailleurs (solo, partie hébergée, serveur d'un autre), rien ne
part : les rapports attendent dans `Storage\sir-cel-shading\Reports`, un fichier
chacun (100 au plus, les plus anciens abandonnés d'abord), d'un lancement du jeu
à l'autre, et partent à la prochaine connexion à PU ou PTU, cinq par seconde au
plus.

**Jamais au prix du jeu.** Écrire, lire ou envoyer un rapport ne lève jamais
d'exception : une faute est avalée, le rapport reste sur le disque et sera
retenté plus tard.

## Nom affiché dans Pulsar

Pulsar lit le nom affiché, la bulle d'aide et la description dans la fiche du
greffon au catalogue Sirius, pas dans ce dépôt. Le studio y recopie
`PluginHub/sir-cel-shading.xml` à chaque publication, avec `FriendlyName` à
« Sir Cel Shading » et le commit publié dans `Commit`. `SourceDirectories`
limite la compilation au dossier `Source`.

## Construction et tests

    dotnet build sir-cel-shading.csproj
    dotnet test tests/tests.csproj

La construction cherche le jeu dans la propriété `Bin64`, puis dans la variable
d'environnement `SE_BIN64`, puis dans les bibliothèques Steam les plus
courantes. Pour un autre emplacement, voir `Directory.Build.props.example`.

Les tests couvrent la logique pure (`Source/Logic`) : les réglages et leur
chargement depuis d'anciens fichiers, la commande de tchat, les arrêts de
session, la cohabitation, les variantes de shader et la source du shader, et
les rapports d'erreurs (leur forme, un rapport par erreur et par session, les
rapports de plantage depuis un journal de jeu, le nettoyage des données du
joueur sur un journal de jeu dans `tests/Data`, la file et ses échecs). Ils
n'ont besoin ni du jeu ni de Pulsar.
