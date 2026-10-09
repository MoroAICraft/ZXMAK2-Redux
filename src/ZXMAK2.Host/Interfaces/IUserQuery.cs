using ZXMAK2.Host.Entities;


namespace ZXMAK2.Host.Interfaces
{
    /// <summary>Optional action-labelled choice; existing query services stay compatible.</summary>
    public interface IActionUserQuery
    {
        DlgResult ShowActions(string message, string caption,
            string acceptText, string discardText, string cancelText);
    }

    public interface IUserQuery
    {
        DlgResult Show(
            string message,
            string caption,
            DlgButtonSet buttonSet,
            DlgIcon icon);
        
        object ObjectSelector(object[] objArray, string caption);

        bool QueryText(
            string caption,
            string text,
            ref string value);

        bool QueryValue(
            string caption,
            string text,
            string format,
            ref int value,
            int min,
            int max);
    }
}
