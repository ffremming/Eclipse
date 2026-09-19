using System.Text;

namespace SpaceGame.Items
{
    /// <summary>
    /// Turns an item asset's name into something a player should read: "LightAxe" becomes "Light Axe".
    ///
    /// <para>
    /// Item assets are named for the file they live in, and every one of them is PascalCase with no
    /// separators. Splitting at the case change is the whole rule — it is not trying to be a
    /// localisation system, and an item that wants a name unlike its asset should be given one on
    /// <see cref="InventoryItem.itemName"/> instead.
    /// </para>
    /// </summary>
    public static class ItemDisplayName
    {
        public static string From(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return string.Empty;

            var spaced = new StringBuilder(assetName.Length + 4);

            for (int i = 0; i < assetName.Length; i++)
            {
                char c = assetName[i];

                // A capital that follows a lower-case letter or a digit starts a new word. Runs of
                // capitals ("UFO") stay together because the previous character is not lower-case.
                bool startsWord = i > 0 && char.IsUpper(c) &&
                                  (char.IsLower(assetName[i - 1]) || char.IsDigit(assetName[i - 1]));

                if (startsWord) spaced.Append(' ');
                spaced.Append(c);
            }

            return spaced.ToString();
        }
    }
}
