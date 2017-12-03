namespace FixApCore
{
    using Jurassic;
    using Jurassic.Library;

    public class Location : ObjectInstance
    {
        public Location(ScriptEngine engine, string href)
            : base(engine)
        {
            this["href"] = href;
            this["search"] = "";
        }
    }
}
