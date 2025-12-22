namespace DbLive.Common;

public static class ListExtentions
{
	public static List<T> RemoveWhere<T>(this List<T> entities, Func<T, bool> predicate)
	{
		lock (entities)
		{
			List<T> removedRecords = entities.Where(predicate).ToList();
			foreach (T entity in removedRecords) entities.Remove(entity);
			return removedRecords;
		}
	}
}
