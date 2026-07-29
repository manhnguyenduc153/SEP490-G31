const ExcelJS = require('exceljs');
const path = require('path');
const os = require('os');

async function createExcel() {
    const workbook = new ExcelJS.Workbook();
    const sheet = workbook.addWorksheet('Test Case');

    sheet.columns = [
        { width: 15 },
        { width: 45 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 },
        { width: 8 }
    ];

    const darkBlueFill = {
        type: 'pattern',
        pattern: 'solid',
        fgColor: { argb: 'FF000080' }
    };
    const whiteFont = { color: { argb: 'FFFFFFFF' }, bold: true, name: 'Calibri' };
    const boldFont = { bold: true, name: 'Calibri' };
    
    sheet.addRow(['Code Module', 'HomeworkService', 'Method', 'CreateHomeworkAsync', '','','','','','','','']);
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

    sheet.addRow(['Condition', 'Precondition', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Can connect with server', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', '']);
    sheet.addRow(['', 'Cannot connect with server', '', '', '', '', '', '', '', '', '', 'O']);
    
    sheet.addRow(['', 'Data Validity', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'All fields are valid (full data)', 'O', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Only required fields valid', '', 'O', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'ClassId does not exist', '', '', 'O', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'TeacherId does not exist', '', '', '', 'O', '', '', '', '', '', '']);
    sheet.addRow(['', 'Title is missing or empty', '', '', '', '', 'O', '', '', '', '', '']);
    sheet.addRow(['', 'ClassId <= 0 or missing', '', '', '', '', '', 'O', '', '', '', '']);
    sheet.addRow(['', 'TeacherId <= 0 or missing', '', '', '', '', '', '', 'O', '', '', '']);
    sheet.addRow(['', 'DueDate is in the past', '', '', '', '', '', '', '', 'O', '', '']);
    sheet.addRow(['', 'TotalScore is negative', '', '', '', '', '', '', '', '', 'O', '']);
    
    sheet.addRow(['Confirm', 'Return', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Success = True', 'O', 'O', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Success = False', '', '', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O']);
    sheet.addRow(['', 'Data is not null', 'O', 'O', '', '', '', '', '', '', '', '']);
    
    sheet.addRow(['', 'Exception', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Throws Exception', '', '', '', '', '', '', '', '', '', 'O']);
    
    sheet.addRow(['', 'Log message', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Thêm bài tập thành công', 'O', 'O', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Lỗi validate / DB', '', '', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O']);
    
    sheet.addRow(['Result', 'Type(N:Normal, A:Abnormal, B:Boundary)', 'N', 'N', 'A', 'A', 'A', 'A', 'A', 'A', 'A', 'A']);
    sheet.addRow(['', 'Passed/Failed', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P']);
    sheet.addRow(['', 'Executed Date', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Defect ID', '', '', '', '', '', '', '', '', '', '']);

    sheet.mergeCells('A8:A20');
    sheet.mergeCells('A21:A29');
    sheet.mergeCells('A30:A33');

    ['A8', 'A21', 'A30'].forEach(cellRef => {
        const cell = sheet.getCell(cellRef);
        cell.fill = darkBlueFill;
        cell.font = whiteFont;
        cell.alignment = { vertical: 'top', horizontal: 'left' };
    });

    const boldRows = [8, 11, 21, 25, 27, 30, 31, 32, 33];
    boldRows.forEach(rowNum => {
        sheet.getCell('B' + rowNum).font = boldFont;
    });

    for(let r=7; r<=33; r++) {
        const row = sheet.getRow(r);
        for(let c=1; c<=12; c++) {
            const cell = row.getCell(c);
            cell.border = {
                top: {style:'thin'},
                left: {style:'thin'},
                bottom: {style:'thin'},
                right: {style:'thin'}
            };
            if(c >= 3 && r >= 8) {
                cell.alignment = { vertical: 'middle', horizontal: 'center' };
                if (cell.value === 'O' || cell.value === 'P' || cell.value === 'N' || cell.value === 'A') {
                    cell.font = boldFont;
                }
            }
        }
    }

    const desktopPath = path.join(os.homedir(), 'Desktop', 'CreateHomeworkAsync_TestCase_v3.xlsx');
    await workbook.xlsx.writeFile(desktopPath);
    console.log(desktopPath);
}
createExcel().catch(console.error);
