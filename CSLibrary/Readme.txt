CSLibrary is CS108 RFID reader framework










API List
--------



Reader Callback event
----------
public event EventHandler<VoltageEventArgs> OnVoltageEvent;										// Return battery voltage
public event EventHandler<HotKeyEventArgs> OnKeyEvent;											// Return key button status
public event EventHandler<CSLibrary.Events.OnReaderStateChangedEventArgs> OnReaderStateChanged;	// Return Reader Status
	Event Type
	----------
	CSLibrary.Constants.ReaderCallbackType.COMMUNICATION_ERROR									// RFID reader no response
	CSLibrary.Constants.ReaderCallbackType.CONNECTION_LOST										// Bluetooth disconnected


Reader API Function call:
-----------
public async Task<bool> ConnectAsync(IAdapter adapter, IDevice device)							// Connect to reader
public async void DisconnectAsync()																// Disconnect from reader


Barcode Callback event
-----------
public event EventHandler<CSLibrary.Barcode.BarcodeEventArgs> OnCapturedNotify;					// Return barcode data
public event EventHandler<CSLibrary.Barcode.BarcodeStateEventArgs> OnStateChanged;				// Return barcode module status
	Event Type
	----------
	CSLibrary.Barcode.Constants.BarcodeState.IDLE												// In idle mode and ready to receive command
	CSLibrary.Barcode.Constants.BarcodeState.BUSY												// Barcode sacnning


Barcode API Function call:
-----------
public bool Start()																				// Start scanning
public bool Stop()																				// Stop scanning
public void FactoryReset()																		// Initialize Barcode setting to Factory Reset


RFID Callback event
-----------
public event EventHandler<CSLibrary.Events.OnAsyncCallbackEventArgs> OnAsyncCallback;			// Return inventory / searching tags data
	Event Type
	----------
	CSLibrary.Constants.CallbackType.TAG_RANGING												// inventory data
	CSLibrary.Constants.CallbackType.TAG_SEARCHING												// searching tag data

public event EventHandler<CSLibrary.Events.OnAccessCompletedEventArgs> OnAccessCompleted;		// Return read/write/lock result
public event EventHandler<CSLibrary.Events.OnStateChangedEventArgs> OnStateChanged;				// Return RFID reader state
	Event Type
	----------
	CSLibrary.Constants.RFState.INITIALIZATION_COMPLETE											// RFID initialization complete
	CSLibrary.Constants.RFState.IDLE															// RFID reader in idle mode and ready to receive command
	CSLibrary.Constants.RFState.BUSY															// RFID reader busy


RFID Properties:
-----------
public uint SelectedChannel																		// Currect selected channel
public RegionCode SelectedRegionCode															// Current selected region code
public bool IsHoppingChannelOnly																// Is reader only hopping channel
public bool IsFixedChannelOnly																	// Is reader only fixed channel
public bool IsFixedChannel																		// Is current selected fixed channel
public Machine DeviceType																		// Reader Type
public ChipSetID ChipSetID																		// Reader chipset ID
public uint CountryCode																			// Reader Counter Code


RFID API Function call:
--------------
public uint GetActiveMaxPowerLevel()															// Get Max Power level
public Result GetPowerLevel(ref uint pwrlvl)													// Get Current Power level
public void SetPowerLevel(UInt32 pwrlevel)														// Set Power level

public uint[] GetActiveLinkProfile()															// Get active profile list
public Result SetCurrentLinkProfile(uint profile)												// Set profile

public Result SetPostMatchCriteria(SingulationCriterion[] postmatch)							// Set Post Filter

public Result SetTagGroup(TagGroup tagGroup)													// Set TagGroup parameters
public Result GetTagGroup(ref TagGroup tagGroup)												// Get TagGroup parameters

public Result SetCurrentSingulationAlgorithm(SingulationAlgorithm SingulationAlgorithm)			// Set current algorithm
public Result GetCurrentSingulationAlgorithm(ref SingulationAlgorithm SingulationAlgorithm)		// Get current algorithm
				
public Result SetFixedQParms(FixedQParms fixedQParm)											// Set fixed Q parameters
public Result GetFixedQParms(ref FixedQParms fixedQ)											// Get current fixed Q parameters 

public Result SetDynamicQParms(DynamicQParms dynParm)											// Set Dynamic Q parameters
public Result GetDynamicQParms(ref DynamicQParms parms)											// Get Dynamic Q parameters

public Result SetOperationMode(RadioOperationMode mode)											// Set operation mode CONTINUOUS/NON-CONTINUOUS
public void GetOperationMode(ref RadioOperationMode mode)										// Get current operation mode

public Result SetOperationMode(ushort cycles)													// Set inventory antenna cycle
public Result SetInventoryDuration(uint duration)												// Set inventory duration (dwell time)
public Result SetInventoryCycle(uint cycle)														// Set inventory cycles count

public Result SetFixedChannel(RegionCode prof = RegionCode.CURRENT, uint channel = 0)			// Set to fixed channel with region
public Result SetHoppingChannels(RegionCode prof)												// Set to hopping with region
public Result SetHoppingChannels()																// Set to hopping with current selected region
public Result SetAgileChannels(RegionCode prof)													// Set to agile channel with region

public Result GetCountryCode(ref uint code)														// Get reader country code
public List<RegionCode> GetActiveRegionCode()													// Get vaild region code list

public double[] GetAvailableFrequencyTable(RegionCode region)									// Get Available frequency table with region code
public double[] GetCurrentFrequencyTable()														// Get frequency table on current selected region

public Result StartOperation(Operation opertion)												// Start special function (see below table)
public void StopOperation()																		// Stop CONTINUOUS inventory 


Operation value:
----------
Operation.TAG_RANGING																			// Start Inventory
Operation.TAG_PRERANGING																		// Inventory pre-setting
Operation.TAG_EXERANGING																		// Execute inventory command
Operation.TAG_SEARCHING																			// Start search Tag
Operation.TAG_SELECTED																			// Set Selected Tag parameter
Operation.TAG_GENERALSELECTED																	// Set Selected Tag parameter
Operation.TAG_PREFILTER																			// Set Pre-Filter
Operation.TAG_READ_PC																			// Start to read PC
Operation.TAG_READ_EPC																			// Start to read EPC
Operation.TAG_READ_ACC_PWD																		// Start to read access password
Operation.TAG_READ_KILL_PWD																		// Start to read kill password
Operation.TAG_READ_TID																			// Start to read TID bank data
Operation.TAG_READ_USER																			// Start to reader USER bank data
Operation.TAG_WRITE_PC																			// Start to Write PC
Operation.TAG_WRITE_EPC																			// Start to Write EPC
Operation.TAG_WRITE_ACC_PWD																		// Start to Write access password
Operation.TAG_WRITE_KILL_PWD																	// Start to Write kill password
Operation.TAG_WRITE_USER																		// Start to Write USER bank data
Operation.TAG_LOCK																				// Set Tag Lock
Operation.TAG_BLOCK_PERMALOCK																	// Set Tag Block Permalock
Operation.TAG_KILL																				// Kill Tag
