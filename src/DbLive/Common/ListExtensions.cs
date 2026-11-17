namespace DbLive.Common;

internal static class ListExtensions
{
	public static List<T> RemoveWhere<T>(this List<T> entities, Func<T, bool> predicate)
	{
		lock (entities)
		{
			List<T> removedRecords = entities.Where(predicate).ToList();
			foreach (T entity in removedRecords)
				_ = entities.Remove(entity);
			return removedRecords;
		}
	}
}
