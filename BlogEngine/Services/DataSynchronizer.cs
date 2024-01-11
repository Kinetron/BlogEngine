using GearShop.Contracts;
using GearShop.Controllers;
using GearShop.Enums;
using GearShop.Models.Entities;
using GearShop.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using Serilog;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GearShop.Services
{
    /// <summary>
    /// Синхронизирует данные БД с внешними данными(например с файлом csv).
    /// </summary>
    public class DataSynchronizer : IDataSynchronizer
	{
		/// <summary>
		/// Шаг через который будет добавлена информация о процессе синхронизации.
		/// </summary>
		private const int StepProgress = 100;
		private readonly GearShopDbContext _dbContext;
		private readonly ILogger<HomeController> _logger;

		/// <summary>
		/// Название источника в таблице InfoSource для синхронизации прайс листа.
		/// </summary>
		private const string PriceSourceName = "Из прайса";

		public DataSynchronizer(GearShopDbContext dbContext, ILogger<HomeController> logger)
		{
			_dbContext = dbContext;
			_logger = logger;
		}

		public string LastError { get; private set; }

		/// <summary>
		/// Синхронизирует данные в БД с файлом CSV.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public async Task<bool> CsvSynchronize(string fileName, string shopName)
		{
			return true;
		}

		/// <summary>
		/// Удаляет продукты которых нет в прайсе.
		/// </summary>
		/// <param name="beginDt"></param>
		/// <param name="infoSourceId"></param>
		private async Task<bool> DeleteEarlyProducts(DateTime beginDt, int infoSourceId, int shopId)
		{
			var products = await _dbContext.Products.Where(p => p.Changed < beginDt
			                                                    && p.InfoSourceId == infoSourceId 
			                                                    && p.ShopId == shopId).ToListAsync();
			products.ForEach(p=>
			{
				p.Deleted = 1;
				p.Changed = DateTime.Now;
			});

			await _dbContext.SaveChangesAsync();

			return true;
		}

		/// <summary>
		/// Синхронизация картинок продуктов.
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public bool ProductImagesSynchronize(string fileName, string storagePath)
		{
			string zipFile = Path.Combine(storagePath, fileName);
			string imageDir = Path.Combine(storagePath, Path.GetFileNameWithoutExtension(fileName));

			try
			{
				CleanDir(imageDir);
				Archivator.UnpackSplitZip(zipFile, imageDir);
				List<string> files = Directory.GetFiles(imageDir, "*.*", SearchOption.AllDirectories).ToList();
				
				foreach (string file in files)
				{
					FileInfo mFile = new FileInfo(file);
					mFile.MoveTo(Path.Combine(@"wwwroot", "productImages", mFile.Name), true);
				}
			}
			catch (Exception ex)
			{
				LastError = $"Исключение {ex.Message} {ex.StackTrace}";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Возвращает информацию о текущей выполняемой операции(например синхронизация данных).
		/// </summary>
		/// <param name="operationId"></param>
		/// <returns></returns>
		public async Task<string> GetOperationStatus(int operationId)
		{
			try
			{
				var info =
					await _dbContext.PriceSynchronizeStatus.FirstOrDefaultAsync(o => o.OperationId == operationId);

				if (info == null) return null;
				return JsonConvert.SerializeObject(info, Formatting.Indented);
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message, ex);
				return null;
			}
		}

		/// <summary>
		/// Удаляет все файлы в директории.
		/// </summary>
		/// <param name="dir"></param>
		private void CleanDir(string dir)
		{
			if (!Directory.Exists(dir)) return;

			foreach (string file in Directory.GetFiles(dir))
			{
				FileInfo fi = new FileInfo(file);
				fi.Delete();
			}
		}
	}
}
