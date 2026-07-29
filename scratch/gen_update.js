const ExcelJS = require('exceljs');
const path = require('path');
const os = require('os');

async function createExcel() {
    const workbook = new ExcelJS.Workbook();
    const sheet = workbook.addWorksheet('Test Case');

    sheet.columns = [
        { width: 15 },
        { width: 45 },
        { width: 8 }, { width: 8 }, { width: 8 }, { width: 8 }, { width: 8 },
        { width: 8 }, { width: 8 }, { width: 8 }, { width: 8 }, { width: 8 }
    ];

    const darkBlueFill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF000080' } };
    const whiteFont = { color: { argb: 'FFFFFFFF' }, bold: true, name: 'Calibri' };
    const boldFont = { bold: true, name: 'Calibri' };
    
    sheet.addRow(['Code Module', 'HomeworkService', 'Method', 'UpdateHomeworkAsync', '','','','','','','','']);
    sheet.addRow(['Created By', '', 'Executed By', '', '','','','','','','','']);
    sheet.addRow(['Test requirement', '', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['Passed', 'Failed', 'Untested', 'N/A/B', 'Total Test Cases', '', '', '', '', '', '', '']);
    sheet.addRow([10, 0, 0, 0, 10, '', '', '', '', '', '', '']);
    sheet.addRow([]);
    
    const row7 = sheet.addRow(['', '', 'UTCID01', 'UTCID02', 'UTCID03', 'UTCID04', 'UTCID05', 'UTCID06', 'UTCID07', 'UTCID08', 'UTCID09', 'UTCID10']);
    for(let i=3; i<=12; i++) {
        const cell = row7.getCell(i);
        cell.fill = darkBlueFill;
        cell.font = whiteFont;
        cell.alignment = { textRotation: -90, vertical: 'middle', horizontal: 'center' };
    }

    let currentRow = 8;
    function addSubHeader(name) {
        sheet.addRow(['', name, '', '', '', '', '', '', '', '', '', '']);
        sheet.getCell('B' + currentRow).font = boldFont;
        currentRow++;
    }
    function addCondition(name, rowMarks) {
        sheet.addRow(['', name, ...rowMarks]);
        currentRow++;
    }

    // CONDITION Section
    sheet.addRow(['Condition', 'Precondition', '', '', '', '', '', '', '', '', '', '']);
    sheet.getCell('B' + currentRow).font = boldFont;
    let conditionStart = currentRow;
    currentRow++;
    
    addCondition('Can connect with server', ['O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', '']);
    addCondition('Cannot connect with server', ['', '', '', '', '', '', '', '', '', 'O']);
    
    addSubHeader('Homework Existence');
    addCondition('Homework exists in DB (IsDeleted = false)', ['O', 'O', '', '', 'O', 'O', 'O', 'O', 'O', 'O']);
    addCondition('Homework does not exist (ID not found)', ['', '', 'O', '', '', '', '', '', '', '']);
    addCondition('Homework exists in DB but IsDeleted = true', ['', '', '', 'O', '', '', '', '', '', '']);
    
    addSubHeader('Homework Data Status (DTO)');
    addCondition('All update fields provided and valid', ['O', 'O', 'O', 'O', '', '', '', 'O', 'O', 'O']);
    addCondition('Title is missing or empty', ['', '', '', '', 'O', '', '', '', '', '']);
    addCondition('DueDate is in the past', ['', '', '', '', '', 'O', '', '', '', '']);
    addCondition('TotalScore is negative', ['', '', '', '', '', '', 'O', '', '', '']);
    addCondition('Database constraint error on update', ['', '', '', '', '', '', '', '', 'O', '']);
    
    let conditionEnd = currentRow - 1;

    // CONFIRM Section
    let confirmStart = currentRow;
    sheet.addRow(['Confirm', 'Return', '', '', '', '', '', '', '', '', '', '']);
    sheet.getCell('B' + currentRow).font = boldFont;
    currentRow++;
    
    addCondition('True (T) / Success', ['O', 'O', '', '', '', '', '', '', '', '']);
    addCondition('False (F) / Failed', ['', '', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O']);
    
    addSubHeader('Exception');
    addCondition('Throws Exception', ['', '', '', '', '', '', '', '', 'O', 'O']);
    
    addSubHeader('Log message');
    addCondition('Cập nhật bài tập thành công', ['O', 'O', '', '', '', '', '', '', '', '']);
    addCondition('Không tìm thấy bài tập', ['', '', 'O', 'O', '', '', '', '', '', '']);
    addCondition('Lỗi validate / DB', ['', '', '', '', 'O', 'O', 'O', 'O', 'O', 'O']);
    
    let confirmEnd = currentRow - 1;

    // RESULT Section
    let resultStart = currentRow;
    sheet.addRow(['Result', 'Type(N : Normal, A : Abnormal, B : Boundary)', 'N', 'N', 'A', 'A', 'A', 'A', 'A', 'N', 'A', 'A']);
    sheet.getCell('B' + currentRow).font = boldFont;
    currentRow++;
    
    addCondition('Passed/Failed', ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P']);
    sheet.getCell('B' + (currentRow-1)).font = boldFont;
    addCondition('Executed Date', ['', '', '', '', '', '', '', '', '', '']);
    sheet.getCell('B' + (currentRow-1)).font = boldFont;
    addCondition('Defect ID', ['', '', '', '', '', '', '', '', '', '']);
    sheet.getCell('B' + (currentRow-1)).font = boldFont;
    let resultEnd = currentRow - 1;

    sheet.mergeCells('A' + conditionStart + ':A' + conditionEnd);
    sheet.mergeCells('A' + confirmStart + ':A' + confirmEnd);
    sheet.mergeCells('A' + resultStart + ':A' + resultEnd);

    ['A' + conditionStart, 'A' + confirmStart, 'A' + resultStart].forEach(cellRef => {
        const cell = sheet.getCell(cellRef);
        cell.fill = darkBlueFill;
        cell.font = whiteFont;
        cell.alignment = { vertical: 'top', horizontal: 'left' };
    });

    for(let r=7; r<=resultEnd; r++) {
        const row = sheet.getRow(r);
        for(let c=1; c<=12; c++) {
            const cell = row.getCell(c);
            cell.border = { top: {style:'thin'}, left: {style:'thin'}, bottom: {style:'thin'}, right: {style:'thin'} };
            if(c >= 3 && r >= 8) {
                cell.alignment = { vertical: 'middle', horizontal: 'center' };
                if (['O', 'P', 'N', 'A', 'B', 'T', 'F'].includes(cell.value)) {
                    cell.font = boldFont;
                }
            }
        }
    }

    const desktopPath = path.join(os.homedir(), 'Desktop', 'UpdateHomeworkAsync_TestCase.xlsx');
    await workbook.xlsx.writeFile(desktopPath);
    console.log(desktopPath);
}
createExcel().catch(console.error);
