# Sir Cel Shading

Greffon c&ocirc;t&eacute; joueur pour Space Engineers, charg&eacute; par Plugin Loader. Il ne fait
qu'une chose : le **cel shading**, un rendu fa&ccedil;on bande dessin&eacute;e. Contours noirs
autour des blocs, du d&eacute;cor et des personnages ; couleurs en aplats.

- Le joueur l'active ou le coupe &agrave; son gr&eacute;, sans relancer le jeu.
- Coup&eacute;, le jeu dessine avec ses propres shaders : l'image est exactement celle
  du jeu.
- Tout se passe sur la machine du joueur. Rien ne passe par le serveur, et un
  joueur sans le greffon voit le rendu du jeu.
- L'interface (HUD, menus, textes) reste nette : elle est dessin&eacute;e apr&egrave;s l'effet.

## Utilisation

- **R&eacute;glages** : Plugin Loader, Sir Cel Shading, bouton des r&eacute;glages. Le
  premier r&eacute;glage est la case **Activer le greffon**. Suivent le nombre de
  teintes, l'&eacute;paisseur et la noirceur des contours, la sensibilit&eacute; aux ar&ecirc;tes
  et la vivacit&eacute; des couleurs. Chaque changement se voit &agrave; l'image suivante.
- **Discussion** : `/cel` bascule le rendu ; `/cel activer`, `/cel couper`,
  `/cel etat`. La commande ne part pas aux autres joueurs.
- **R&eacute;glages enregistr&eacute;s** dans `%AppData%\SpaceEngineers\Storage\sir-cel-shading\reglages.xml`.

## Fonctionnement

Le jeu livre ses effets d'image en source (`Content/Shaders`) et les compile
lui-m&ecirc;me au chargement. Sir Cel Shading s'accroche &agrave; l'&eacute;tape des couleurs
finales, `MyToneMapping.Run`, qui est un compute shader
(`Postprocess/Tonemapping/Main.hlsl`) en trois variantes : `m_cs`,
`m_csAlphaLuminance`, `m_csSkip`.

1. Au lancement, le greffon &eacute;crit sa variante de ce shader,
   `Storage\sir-cel-shading\Shaders\CelShading.hlsl`. C'est le corps du jeu
   repris &agrave; l'identique (grain, exposition, halo, courbe filmique, filtres),
   suivi des aplats et des contours, juste avant la conversion en sRGB. Elle
   inclut les en-t&ecirc;tes du jeu entre chevrons, donc ceux du dossier de shaders
   du jeu.
2. Activ&eacute;, un pr&eacute;fixe Harmony compile les trois variantes avec le compilateur
   du jeu (`MyShaderCompiler.Compile`, qui refuse sans planter, puis
   `MyComputeShaders.Create`). Il place ensuite la variante en cours dans le
   champ statique du jeu, et lie la profondeur de la sc&egrave;ne
   (`MyGBuffer.Main.ResolvedDepthStencil.SrvDepth`) en `t31`. Le postfixe rend
   au champ le shader du jeu et d&eacute;lie `t31`. Un finaliseur fait de m&ecirc;me si le
   passage &eacute;choue.
3. Coup&eacute;, le pr&eacute;fixe ne touche &agrave; rien.

**Contours.** Ils sont tir&eacute;s de la d&eacute;riv&eacute;e seconde de l'inverse de la
distance : elle est nulle sur une surface plane, et s'allume aux silhouettes et
aux ar&ecirc;tes. La mesure est rapport&eacute;e &agrave; la distance la plus proche du voisinage
et &agrave; l'angle d'un pixel. Elle raisonne donc en rapport de distances, jamais en
m&egrave;tres : une ar&ecirc;te se dessine pareil &agrave; un m&egrave;tre ou &agrave; dix kilom&egrave;tres. Le ciel
est reconnu &agrave; la profondeur de d&eacute;gagement du jeu. La d&eacute;tection de contours du
jeu (`Postprocess/EdgeDetection.hlsl`) ne sert pas : elle ne marque que la
couverture de l'anticr&eacute;nelage multi-&eacute;chantillons, que le jeu n'active plus.

**Aplats.** La valeur de chaque couleur (son canal le plus fort, en sRGB) tombe
sur un nombre r&eacute;glable de paliers ; la teinte est gard&eacute;e. Sous la moiti&eacute; du
premier palier, l'image reste celle du jeu : le noir de l'espace reste noir.

**Emplacement t31.** Aucun fichier de `Content/Shaders` ne d&eacute;clare `t31`. Le
passage du jeu n'utilise que `t0` &agrave; `t3`, `u0` et `s0` &agrave; `s3`, et le moteur
g&egrave;re 32 emplacements par &eacute;tage.

**Co&ucirc;t.** Neuf lectures de profondeur et quelques op&eacute;rations par pixel, dans
un passage que le jeu ex&eacute;cute de toute fa&ccedil;on. Il ne s'ajoute aucun passage
plein &eacute;cran.

## Arr&ecirc;ts et cohabitation

Tout nom interne du moteur est r&eacute;solu par r&eacute;flexion au lancement. Plusieurs cas
arr&ecirc;tent l'effet pour toute la session :

- un nom introuvable ;
- un en-t&ecirc;te du jeu absent ;
- une variante refus&eacute;e par le compilateur du jeu ;
- une anomalie sur le fil de rendu.

Tous passent par le m&ecirc;me chemin (`ArretDeSession`). Le jeu garde son rendu, une
ligne `[sir-cel-shading]` part au journal du jeu, et le joueur re&ccedil;oit une
notification d&egrave;s qu'une partie est ouverte.

Deux greffons ne se disputent jamais une m&ecirc;me &eacute;tape. Avant de se brancher, puis
toutes les dix secondes, Sir Cel Shading regarde qui est accroch&eacute; &agrave;
`MyToneMapping.Run` (`Harmony.GetPatchInfo`). S'il trouve un autre propri&eacute;taire,
il c&egrave;de la place et le dit au joueur.

## Nom affich&eacute; dans Plugin Loader

Plugin Loader lit le nom affich&eacute; dans la fiche du greffon, d&eacute;pos&eacute;e dans son
d&eacute;p&ocirc;t PluginHub, et non dans ce d&eacute;p&ocirc;t. La fiche &agrave; d&eacute;poser est
`PluginHub/sir-cel-shading.xml`, avec `FriendlyName` &agrave; &laquo; Sir Cel Shading &raquo;. Son
champ `Commit` se remplit &agrave; la publication. `SourceDirectories` limite la
compilation au dossier `Source`.

## Compiler et tester

    dotnet build sir-cel-shading.csproj
    dotnet test tests/tests.csproj

Le build cherche le jeu dans la propri&eacute;t&eacute; `Bin64`, puis dans la variable
d'environnement `SE_BIN64`, puis dans les biblioth&egrave;ques Steam les plus
courantes. Pour un autre emplacement, voir `Directory.Build.props.example`.

Les tests portent sur la logique pure (`Source/Logique`) : r&eacute;glages, commande,
arr&ecirc;t de session, cohabitation, variantes et source du shader. Ils ne demandent
ni le jeu ni Plugin Loader.
