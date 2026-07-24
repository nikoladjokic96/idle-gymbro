namespace IdleGymBro.Data
{
    // Int values double as SpriteRenderer sortingOrder — depth order from CLAUDE.md §7.
    public enum CharacterLayer
    {
        Background = -10,
        Body = 0,
        Shorts = 10,
        Shoes = 20,
        Shirt = 30,
        Arms = 40,
        Head = 50,
        Beard = 60,
        Hair = 70,
        Accessory = 80
    }
}
