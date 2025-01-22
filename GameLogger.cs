namespace Blockgame_VulkanTests;
public enum SeverityType
{
        
    Info,
    Warning,
    Error
        
}
public class GameLogger
{
    
    public static void DebugLog(string message, SeverityType severity = SeverityType.Info)
    {

        switch (severity)
        {
            case SeverityType.Warning:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("[Warning] ");
                break;
            case SeverityType.Error:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[Error] ");
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("[Info] ");
                break;
        }

        Console.ResetColor();
        Console.WriteLine(message);

    }

    public static void ThrowError(string message)
    {
        
        DebugLog(message, SeverityType.Error);
        throw new Exception(message);

    }
    
}