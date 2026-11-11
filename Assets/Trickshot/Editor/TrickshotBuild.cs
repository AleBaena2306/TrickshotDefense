using KovusPride.Publishing;
using KovusPride.Web.Editor;
using UnityEditor;

// TRICKSHOT > Build: web para el portfolio (móvil + PC) y builds para cada portal, con el framework de Kovu's Pride.
// No se publica nada sin el visto bueno de Alejandro: «Web (móvil + PC)» sube la build al portfolio.
public static class TrickshotBuild
{
    private static readonly WebGame Web = new WebGame("trickshot-defense", "Trickshot Defense");
    private const string Game = "Trickshot Defense";

    [MenuItem("TRICKSHOT/Build/Web: prueba rápida (solo PC, sin publicar)", priority = 1)]
    public static void Quick() => KovusPrideWebBuild.QuickTest(Web);

    [MenuItem("TRICKSHOT/Build/Web: solo compilar (sin publicar)", priority = 2)]
    public static void WebOnly() => KovusPrideWebBuild.BuildOnly(Web);

    [MenuItem("TRICKSHOT/Build/Web (móvil + PC) · publicar en el portfolio", priority = 3)]
    public static void WebPublish() => KovusPrideWebBuild.BuildAndPublish(Web);

    [MenuItem("TRICKSHOT/Build/Web: publicar la última build", priority = 4)]
    public static void WebPublishLast() => KovusPrideWebBuild.PublishOnly(Web);

    [MenuItem("TRICKSHOT/Build/CrazyGames", priority = 100)]
    public static void CrazyGames() => KovusPridePublish.Build(Store.CrazyGames, Game);

    [MenuItem("TRICKSHOT/Build/Playgama", priority = 101)]
    public static void Playgama() => KovusPridePublish.Build(Store.Playgama, Game);

    [MenuItem("TRICKSHOT/Build/Playgama · subir la última build", priority = 111)]
    public static void PlaygamaUpload() => KovusPridePublish.UploadLast(Store.Playgama, Game);

    [MenuItem("TRICKSHOT/Build/GameDistribution", priority = 102)]
    public static void GameDistribution() => KovusPridePublish.Build(Store.GameDistribution, Game);

    [MenuItem("TRICKSHOT/Build/itch.io", priority = 103)]
    public static void Itch() => KovusPridePublish.Build(Store.Itch, Game);

    [MenuItem("TRICKSHOT/Build/Todos los portales", priority = 120)]
    public static void All() => KovusPridePublish.BuildAll(Game);
}
