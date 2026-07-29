const ExcelJS = require('exceljs');
const path = require('path');
const os = require('os');

async function createExcel() {
    const workbook = new ExcelJS.Workbook();
    const sheet = workbook.addWorksheet('Test Case');

    sheet.columns = [
        { width: 15 },
        { width: 45 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 },
        { width: 10 }
    ];

    sheet.addRow(['Code Module', 'HomeworkService', 'Method', 'CreateHomeworkAsync', '','','','','','','','']);
    sheet.addRow(['Created By', '', 'Executed By', '', '','','','','','','','']);
    sheet.addRow(['Test requirement', '', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['Passed', 'Failed', 'Untested', 'N/A/B', 'Total Test Cases', '', '', '', '', '', '', '']);
    sheet.addRow([10, 0, 0, 0, 10, '', '', '', '', '', '', '']);
    sheet.addRow([]);
    sheet.addRow(['', '', 'UTCID01', 'UTCID02', 'UTCID03', 'UTCID04', 'UTCID05', 'UTCID06', 'UTCID07', 'UTCID08', 'UTCID09', 'UTCID10']);
    
    sheet.addRow(['Condition', 'Precondition', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Can connect with server', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O', '']);
    sheet.addRow(['', 'Cannot connect with server', '', '', '', '', '', '', '', '', '', 'O']);
    
    sheet.addRow(['', 'Data Validity', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'All fields are valid (full data)', 'O', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Only required fields valid (optional empty)', '', 'O', '', '', '', '', '', '', '', '']);
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
    sheet.addRow(['', 'Thêm bài t?p thành công', 'O', 'O', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'L?i validate / DB', '', '', 'O', 'O', 'O', 'O', 'O', 'O', 'O', 'O']);
    
    sheet.addRow(['Result', 'Type(N:Normal, A:Abnormal, B:Boundary)', 'N', 'N', 'A', 'A', 'A', 'A', 'A', 'A', 'A', 'A']);
    sheet.addRow(['', 'Passed/Failed', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P']);
    sheet.addRow(['', 'Executed Date', '', '', '', '', '', '', '', '', '', '']);
    sheet.addRow(['', 'Defect ID', '', '', '', '', '', '', '', '', '', '']);

    const desktopPath = path.join(os.homedir(), 'Desktop', 'CreateHomeworkAsync_TestCase_v2.xlsx');
    await workbook.xlsx.writeFile(desktopPath);
    console.log(desktopPath);
}

createExcel().catch(console.error);
