using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// Block data service implementation
    /// </summary>
    public class BlockDataService : IBlockDataService
    {
        /// <summary>
        /// Extract data from selected blocks
        /// </summary>
        /// <param name="blockIds">Collection of block object IDs</param>
        /// <param name="transaction">Database transaction</param>
        /// <returns>Operation result containing block data and all attribute tags</returns>
        public OperationResult<(Dictionary<ObjectId, Dictionary<string, string>> BlockData, HashSet<string> AllAttributeTags)> ExtractBlockData(
            IEnumerable<ObjectId> blockIds, 
            Transaction transaction)
        {
            if (blockIds == null)
                return OperationResult<(Dictionary<ObjectId, Dictionary<string, string>>, HashSet<string>)>.ErrorResult("Block ID collection is null", TimeSpan.Zero);
                
            if (transaction == null)
                return OperationResult<(Dictionary<ObjectId, Dictionary<string, string>>, HashSet<string>)>.ErrorResult("Transaction object is null", TimeSpan.Zero);
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                HashSet<string> allAttribTags = new HashSet<string>();
                Dictionary<ObjectId, Dictionary<string, string>> blockData = new Dictionary<ObjectId, Dictionary<string, string>>();
                
                // Add basic property names to collection
                allAttribTags.Add("BlockName");
                allAttribTags.Add("X");
                allAttribTags.Add("Y");
                allAttribTags.Add("Z");
                
                foreach (ObjectId blockId in blockIds)
                {
                    BlockReference blockRef = transaction.GetObject(blockId, OpenMode.ForRead) as BlockReference;
                    if (blockRef == null) {continue;}
                    
                    // Get block definition name
                    BlockTableRecord blockDef = transaction.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    string blockName = blockDef.Name;
                    
                    // Create attribute dictionary for this block
                    Dictionary<string, string> attribValues = new Dictionary<string, string>();
                    attribValues["BlockName"] = blockName;
                    attribValues["X"] = blockRef.Position.X.ToString("0.000");
                    attribValues["Y"] = blockRef.Position.Y.ToString("0.000");
                    attribValues["Z"] = blockRef.Position.Z.ToString("0.000");
                    
                    // Extract attribute values
                    foreach (ObjectId attId in blockRef.AttributeCollection)
                    {
                        AttributeReference attRef = transaction.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (attRef != null)
                        {
                            string tag = attRef.Tag;
                            string value = attRef.TextString;
                            
                            // Add to dictionary and collection
                            attribValues[tag] = value;
                            allAttribTags.Add(tag);
                        }
                    }
                    
                    // Save data for this block
                    blockData[blockRef.ObjectId] = attribValues;
                }
                
                TimeSpan duration = DateTime.Now - startTime;
                var result = (BlockData: blockData, AllAttributeTags: allAttribTags);
                return OperationResult<(Dictionary<ObjectId, Dictionary<string, string>>, HashSet<string>)>.SuccessResult(result, duration);
            }
            catch (System.Exception ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult<(Dictionary<ObjectId, Dictionary<string, string>>, HashSet<string>)>.ErrorResult(ex.Message, duration);
            }
        }
    }
} 