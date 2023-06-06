namespace FFmpegAvalonia;

public class PropertyReflection
{
    public string GetPropVal(string propertyName)
    {
        object? val = this.GetType().GetProperty(propertyName).GetValue(this);
        if (val == null)
        {
            return "null";
        }
        else
        {
            return val.ToString() == null ? "null" : val.ToString()!;
        }
    }
}
