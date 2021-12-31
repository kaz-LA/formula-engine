using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FormulaEngine.Core.Enums;
using FormulaEngine.Core.Interfaces;

namespace FormulaEngine.Data
{
    public class CalculatedFieldRepository : RepositoryBase<ReportingContext>, ICalculatedFieldRepository
    {
        public const int CalculatedFieldsTable = -1;
        public const int CalculatedFieldsUIGroup = -1;
        public const int CalculatedFieldsEntityId = -1;

        public CalculatedFieldRepository(IReportingContextProvider contextProvider, IUser user, IReportingUtilsProvider utilsProvider) : base(
            contextProvider, user, utilsProvider)
        {
        }

        public async Task<ICollection<IFunction>> GetFunctions()
        {
            using (var context = await CreateContext())
            {
                return context.Functions.Where(f => f.IsActive).Include(f => f.Parameters).OrderBy(o => o.Name).ToList<IFunction>();
            }
        }

        public async Task<ICollection<IDataType>> GetDataTypes()
        {
            using (var context = await CreateContext())
            {
                return await context.Set<FunctionDataType>().ToListAsync<IDataType>();
            }
        }

        public async Task<IColumn> FindColumnByTitle(string entityTitle, string columnTitle)
        {
            using (var context = await CreateContext())
            {
                // multiple columns can have the same DefaultTitle/name - so here's the complex logic to 
                // choose the best possible column by it's title/name and optional entity name.
                //   * if entity is not specified => highest preference is given to user's own Calculated field, then a random public field
                //   * else (i.e. entity is specified) => choose the column that belongs to the specified entity e.g. Successor.User Full Name

                var userId = UserId;
                var entitySpecified = !string.IsNullOrEmpty(entityTitle);

                var column = (
                        from c in context.Columns
                        join ugc in context.UIGroupColumns on c.Id equals ugc.ColumnId
                        join ug in context.UIGroups on ugc.GroupId equals ug.Id
                        from e in context.ReportEntities
                                        .Where(e1 => ug.EntityId == e1.Id || ug.EntityId == e1.ParentEntityId)
                                        .DefaultIfEmpty()
                        from cf in context.CalculatedFields.Where(cf1 => c.Id == cf1.ColumnId).DefaultIfEmpty()
                        where (c.DefaultTitle == columnTitle || c.Name == columnTitle) &&
                              (!entitySpecified || e.Title == entityTitle) &&
                              (cf.CreatedUserId == userId || cf.IsPublic || cf.Id == null)
                        orderby c.IsSelectable descending, c.Active descending, c.ColumnDataTypeId descending,
                                cf.IsPublic, cf.IsActive descending
                        select new ColumnInfo
                        {
                            EntityName = e.Title,
                            EntityId = e.Id,
                            ColumnId = c.Id,
                            Name = c.Name,
                            DefaultTitle = c.DefaultTitle,
                            ColumnDataType = c.ColumnDataTypeId,
                            CalculatedFieldId = cf.Id,
                            CalculatedFieldType = cf.OutputTypeId,
                            ColumnDisplayType = c.ColumnDisplayTypeId,
                            IsSelectable = c.IsSelectable,
                            IsActive = c.Active,
                            CalculatedFieldIsActive = cf.IsActive,
                            CalculatedFieldCreatedBy = cf.CreatedUserId,
                            CalculatedFieldIsPublic = cf.IsPublic
                        })
                    .FirstOrDefault();

                return column;
            }
        }

        public async Task<IColumn> GetColumnById(int columnId, int entityId)
        {
            using (var context = await CreateContext())
            {
                var query = from c in context.Columns
                            join ugc in context.UIGroupColumns on c.Id equals ugc.ColumnId
                            join ug in context.UIGroups on ugc.GroupId equals ug.Id
                            from e in context.ReportEntities
                                            .Where(e1 => ug.EntityId == e1.Id || ug.EntityId == e1.ParentEntityId)
                                            .DefaultIfEmpty()
                            from cf in context.CalculatedFields.Where(cf1 => c.Id == cf1.ColumnId).DefaultIfEmpty()
                            where c.Id == columnId && (entityId == 0 || e.Id == entityId)
                            orderby e.ParentEntityId
                            select new ColumnInfo
                            {
                                EntityId = e.Id,
                                EntityName = e.Title,
                                ColumnId = c.Id,
                                Name = c.Name,
                                DefaultTitle = c.DefaultTitle,
                                ColumnDataType = c.ColumnDataTypeId,
                                CalculatedFieldId = cf.Id,
                                CalculatedFieldType = cf.OutputTypeId,
                                ColumnDisplayType = c.ColumnDisplayTypeId,
                                IsSelectable = c.IsSelectable,
                                IsActive = c.Active,
                                CalculatedFieldIsActive = cf.IsActive,
                                CalculatedFieldCreatedBy = cf.CreatedUserId,
                                CalculatedFieldIsPublic = cf.IsPublic
                            };
                return query.FirstOrDefault();
            }
        }

        public async Task<ICalculatedField> GetCalculatedFieldWithReferencedColumns(int calculatedFieldId)
        {
            using (var context = await CreateContext())
            {
                var field = await context.CalculatedFields
                    .AsNoTracking()
                    .Include(cf => cf.ReferencedColumns)
                    .Include(cf => cf.Column)
                    .FirstOrDefaultAsync(f => f.Id == calculatedFieldId && (f.IsPublic || f.CreatedUserId == UserId) && f.IsActive);

                return SetUser(field, context);
            }
        }

        public async Task<ICalculatedField> GetCalculatedField(int calculatedFieldId)
        {
            using (var context = await CreateContext())
            {
                var field = context.CalculatedFields.AsNoTracking()
                    .Include(cf => cf.Column)
                    .FirstOrDefault(f => f.Id == calculatedFieldId && (f.IsPublic || f.CreatedUserId == UserId) && f.IsActive);

                return SetUser(field, context);
            }
        }

        public async Task<TResult> GetCalculatedFieldValue<TResult>(int calculatedFieldId,
            Expression<Func<CalculatedField, TResult>> resultSelector)
        {
            using (var context = await CreateContext())
            {
                return await context.CalculatedFields.AsNoTracking()
                    .Where(f => f.Id == calculatedFieldId)
                    .Select(resultSelector)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<bool> ReferencesAnotherCalculatedField(int calculatedFieldId)
        {
            using (var context = await CreateContext())
            {
                var qry = from cfc in context.CalculatedFieldColumns
                          join c in context.Columns on cfc.ColumnId equals c.Id
                          where cfc.CalculatedFieldId == calculatedFieldId &&
                                c.ColumnDataTypeId == ColumnDataType.CalculatedField
                          select 1;

                return qry.Any();
            }
        }

        public async Task<ICollection<ICalculatedField>> GetCalculatedFields(IUser user)
        {
            using (var context = await CreateContext())
            {
                var fields = await context.CalculatedFields
                    .Where(f => (f.IsPublic || f.CreatedUserId == user.UserId) && f.IsActive)
                    .Include(cf => cf.ReferencedColumns)
                        .ThenInclude(cf => cf.Entity)
                    .Include(cf => cf.ReferencedColumns)
                        .ThenInclude(cf => cf.Column)
                    .Include(cf => cf.Column)
                    .OrderBy(f => f.Name)
                    .ToListAsync<ICalculatedField>();

                var propIds = await ModelUtils.GetAvailableComponentPropertyIds();
                fields = fields.Where(f => f.ReferencedColumns.Where(rc => rc.EntityId > 0).
                    All(rc => user.ManagePermissions.Contains(rc.Entity.ManagePermissionId) && 
                              (rc.ColumnId < 0 || rc.Column.ComponentPropertyId == null || 
                                propIds.Contains(Convert.ToInt32(rc.Column.ComponentPropertyId))))).ToList();

                ModelUtils.FilterBySpecialPermissions(fields);

                return await IncludeUserDetails(fields, context);
            }
        }

        public async Task<ICalculatedField> CreateCalculatedField(ICalculatedField calculatedFieldData)
        {
            var calculatedField = (CalculatedField)calculatedFieldData;
            calculatedField.Id = 0;
            calculatedField.CreatedDate = DateTime.UtcNow;
            calculatedField.CreatedUserId = UserId;
            calculatedField.ModifiedDate = null;
            calculatedField.IsActive = true;
            calculatedField.IsChartable = true; // is there a special logic to set this?

            var referencesStripHtmlField = await ReferencesStripHtmlColumn(calculatedField);
            var column = new Column
            {
                ColumnDataTypeId = ColumnDataType.CalculatedField,
                ColumnDisplayTypeId = ToColumnDisplayType(calculatedField.OutputTypeId, referencesStripHtmlField),
                ColumnFilterTypeId = ToColumnFilterType(calculatedField.OutputTypeId, calculatedField.DecimalPoints),
                DefaultTitle = calculatedField.Name,
                Name = calculatedField.Name,
                TableId = CalculatedFieldsTable,
                IsFilterable = !calculatedField.IsAggregate,
                IsSelectable = true,
                Active = true
            };

            column.UIGroupColumns = new[] { new UIGroupColumn { GroupId = CalculatedFieldsUIGroup, Column = column } };

            calculatedField.Column = column;

            using (var context = await CreateContext())
            {
                await IncludeColumnsFromReferencedCalculatedFields(calculatedField, context);

                context.CalculatedFields.Add(calculatedField);
                await context.SaveChangesAsync();

                return SetUser(calculatedField, context);
            }
        }

        public async Task<ICalculatedField> UpdateCalculatedField(ICalculatedField updatedCalculatedField)
        {
            var updated = (CalculatedField)updatedCalculatedField;

            var currentValues =
                await GetCalculatedFieldValue(updated.Id,
                    cf => new { cf.Expression, cf.Name, cf.OutputTypeId, cf.CreatedUserId, cf.IsPublic });

            if (currentValues == null)
                return null;

            var canUpdate = currentValues.CreatedUserId == UserId ||
                           (currentValues.IsPublic && User.HasManageGlobalCalculatedFieldsAccess);
            if (!canUpdate)
                throw new InvalidOperationException("EX.RPT.CalculatedFieldUpdateDenied");

            var updateColumn = currentValues.Name != updated.Name ||
                               currentValues.OutputTypeId != updated.OutputTypeId;

            var updateReferencedColumns = currentValues.Expression != updated.Expression;

            using (var context = await CreateContext(canReUseExisting: false))
            using (var transaction = context.Database.BeginTransaction())
            {
                try
                {
                    var referencesStripHtmlField = await ReferencesStripHtmlColumn(updated, context);
                    if (updateColumn)
                        await context.Columns.Where(c => c.Id == updated.ColumnId).UpdateAsync(column =>
                            new Column
                            {
                                Name = updated.Name,
                                ColumnDisplayTypeId = ToColumnDisplayType(updated.OutputTypeId, referencesStripHtmlField),
                                ColumnFilterTypeId = ToColumnFilterType(updated.OutputTypeId,
                                    updated.DecimalPoints),
                                DefaultTitle = updated.Name,
                                IsFilterable = !updated.IsAggregate
                            });

                    if (updateReferencedColumns)
                    {
                        var otherCalculatedFieldsReferencingThis = await context.CalculatedFieldColumns
                            .Where(cfc => cfc.ParentId == updated.Id)
                            .Select(c => c.CalculatedFieldId)
                            .Distinct()
                            .ToListAsync();

                        await context.CalculatedFieldColumns
                            .Where(cfc => cfc.CalculatedFieldId == updated.Id || cfc.ParentId == updated.Id)
                            .DeleteAsync();

                        await IncludeColumnsFromReferencedCalculatedFields(updated, context);
                        var addToOthers = CartesianProduct(updated.ReferencedColumns, otherCalculatedFieldsReferencingThis, updated);

                        context.CalculatedFieldColumns.AddRange(updated.ReferencedColumns);
                        context.CalculatedFieldColumns.AddRange(addToOthers);
                    }
                    else
                    {
                        updated.ReferencedColumns = null; // didn't change -- otherwise EF throws exception
                    }

                    updated.Column = null;
                    updated.ModifiedDate = DateTime.UtcNow;

                    context.SetModified(updated);

                    await context.SaveChangesAsync();

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return await GetCalculatedFieldWithReferencedColumns(updated.Id);
        }

        public async Task UpdateRelatedCalculatedFields(int calculatedFieldId, Func<string, Task<ICalculatedField>> formulaParser)
        {
            using (var context = await CreateContext())
            {
                var relatedCalculatedFields = await context.CalculatedFieldColumns
                                .Where(cfc => cfc.ParentId == calculatedFieldId)
                                .Select(c => new { Id = c.CalculatedFieldId, Formula = c.CalculatedField.Expression })
                                .Distinct()
                                .ToListAsync();

                if (!relatedCalculatedFields.Any())
                    return;

                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        foreach (var calcField in relatedCalculatedFields)
                        {
                            var result = await formulaParser(calcField.Formula); /// TODO: need to pass more details, such as decimalPoints for more accurate parsing
                            if (result != null)
                            {
                                await context.CalculatedFields
                                            .Where(cf => cf.Id == calcField.Id)
                                            .UpdateAsync(cf => new CalculatedField { IsAggregate = result.IsAggregate, AggregationTypeId = result.AggregationTypeId });
                                ///TODO: also update Column.IsFilterable
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
                
        private static async Task IncludeColumnsFromReferencedCalculatedFields(
            CalculatedField calculatedField, ReportingContext context)
        {
            var calculatedFieldIds = 
                    calculatedField.ReferencedColumns
                    .Where(r => r.ParentId != null)
                    .Select(r => (int)r.ParentId)
                    .ToList();

            if (!calculatedFieldIds.Any())
                return;

            var columns = await context.CalculatedFieldColumns
                .Where(c => calculatedFieldIds.Contains(c.CalculatedFieldId))
                .ToListAsync();
            
            columns.ForEach(c => calculatedField.ReferencedColumns.Add(
                CalculatedFieldColumn.Create(calculatedField.Id, c))
            );
        }

        private static IEnumerable<CalculatedFieldColumn> CartesianProduct(
            ICollection<CalculatedFieldColumn> columns, 
            ICollection<int> calculatedFieldIds, 
            ICalculatedField updated)
        {
            return columns.SelectMany(c => calculatedFieldIds.Select(id => CalculatedFieldColumn.Create(id, c)))
                          .Union(calculatedFieldIds.Select(id =>
                            CalculatedFieldColumn.Create(id, updated.ColumnId, CalculatedFieldsEntityId, updated.Id))
                          );
        }

        public async Task<ActionResult> DeleteCalculatedField(int calculatedFieldId)
        {
            try
            {
                using (var context = await CreateContext(canReUseExisting: false))
                {
                    // Calc. field should be owned by the user - can't delete another user's calc. field                    
                    var field = await context.CalculatedFields
                        .Where(cf => cf.Id == calculatedFieldId)
                        .Select(cf => new { cf.ColumnId, cf.CreatedUserId, cf.IsPublic })
                        .FirstOrDefaultAsync();

                    if (field == null)
                        return "EX.RPT.CalculatedFieldNotFound";

                    var canDelete = field.CreatedUserId == UserId || (field.IsPublic && User.HasManageGlobalCalculatedFieldsAccess);
                    if (!canDelete)
                        return "EX.RPT.CalculatedFieldDeleteDenied";

                    var isReferencedByAnotherCalculatedField = 
                        await context.CalculatedFieldColumns.AnyAsync(c => c.ColumnId == field.ColumnId && c.CalculatedField.IsActive);

                    if (isReferencedByAnotherCalculatedField)
                        return "EX.RPT.CalculatedFieldIsInUse";

                    await context.CalculatedFields
                            .Where(cf => cf.Id == calculatedFieldId)
                            .UpdateAsync(cf => new CalculatedField { IsActive = false, ModifiedDate = DateTime.UtcNow });

                    await context.Columns
                            .Where(c => c.Id == field.ColumnId)
                            .UpdateAsync(cf => new Column { Active = false });

                    return true;
                }
            }
            catch (Exception ex)
            {
                return "EX.RPT.CalculatedFieldIsInUse";
            }
        }

        public async Task<int> GetCalculatedFieldId(string name)
        {
            using (var context = await CreateContext())
            {
                var userId = UserId;
                return context.CalculatedFields
                    .Where(cf => cf.Name == name && (cf.IsPublic || cf.CreatedUserId == userId) && cf.IsActive)
                    .Select(cf => cf.Id)
                    .FirstOrDefault();
            }
        }

        internal static ColumnDisplayTypeEnum ToColumnDisplayType(DataType dataType, bool referencesStripHtmlField)
        {
            switch (dataType)
            {
                case DataType.Boolean:
                    return ColumnDisplayTypeEnum.BooleanYesNo;
                case DataType.Datetime:
                    return ColumnDisplayTypeEnum.DateTime;
                case DataType.Date:
                    return ColumnDisplayTypeEnum.Date;
                case DataType.AbsoluteDatetime:
                    return ColumnDisplayTypeEnum.AbsoluteDateTime;
                case DataType.AbsoluteDate:
                    return ColumnDisplayTypeEnum.AbsoluteDate;
                case DataType.Number:
                    return ColumnDisplayTypeEnum.Decimal;
                default:
                    return dataType == DataType.String && referencesStripHtmlField
                        ? ColumnDisplayTypeEnum.StripHtml
                        : ColumnDisplayTypeEnum.String;
            }
        }

        internal static ColumnFilterTypeEnum ToColumnFilterType(DataType dataType, int? decimalPoints)
        {
            switch (dataType)
            {
                case DataType.Boolean:
                    return ColumnFilterTypeEnum.Boolean;                
                case DataType.Datetime:
                case DataType.AbsoluteDatetime:
                    return ColumnFilterTypeEnum.DateTime;
                case DataType.Date:
                case DataType.AbsoluteDate:
                    return ColumnFilterTypeEnum.Date;
                case DataType.Number:
                    return ColumnFilterTypeEnum.Decimal;
                default:
                    return ColumnFilterTypeEnum.String;
            }
        }

        private static async Task<ICollection<ICalculatedField>> IncludeUserDetails(List<ICalculatedField> fields,
            ReportingContext context)
        {
            if (!fields.Any())
                return fields;

            var userIds = fields.Select(f => f.CreatedUserId).Distinct();
            var users = await context.Users.Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId);

            fields.ForEach(f =>
            {
                if (users.TryGetValue(f.CreatedUserId, out var user))
                    f.CreatedUser = user;
            });

            return fields;
        }

        private static ICalculatedField SetUser(ICalculatedField field, ReportingContext context)
        {
            if (field != null)
                field.CreatedUser = context.Users.FirstOrDefault(u => u.UserId == field.CreatedUserId);
            return field;
        }

        private async Task<bool> ReferencesStripHtmlColumn(ICalculatedField calculatedField,
            ReportingContext context = null)
        {
            if (calculatedField.OutputTypeId != DataType.String)
                return false;

            context ??= await CreateContext();
            var referencedColumnIds = calculatedField.ReferencedColumns?.Select(c => c.ColumnId)?.ToArray();
            if (referencedColumnIds?.Any() == false)
                return false;

            return await context.Columns.AnyAsync(c =>
                referencedColumnIds.Contains(c.Id) && c.ColumnDisplayTypeId == ColumnDisplayTypeEnum.StripHtml);
        }
    }
}